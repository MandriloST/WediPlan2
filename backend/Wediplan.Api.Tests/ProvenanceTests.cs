using System;
using System.Collections.Generic;
using System.Linq;
using Wediplan.Api.Domain;
using Wediplan.Api.Import;
using Wediplan.Api.Services;
using Xunit;

namespace Wediplan.Api.Tests;

/// <summary>
/// §Zadatak 17 — porijeklo podataka i privola: parsiranje stupaca iz Excela (<see cref="ProvenanceRules"/>), primjena na
/// pružatelja (<see cref="ProvenanceMerge"/>) i privola putem claima (<see cref="ConsentRules"/>). Čiste jedinice, bez baze i Excela.
/// </summary>
public class ProvenanceTests
{
    private static ProvenanceInput Parse(IDictionary<string, string> row, out List<string> warnings)
    {
        var w = new List<string>();
        var input = ProvenanceRules.Parse(name => row.TryGetValue(name, out var v) ? v : "", w);
        warnings = w;
        return input;
    }

    private static ProvenanceInput Parse(params (string Col, string Val)[] cells) =>
        Parse(cells.ToDictionary(c => c.Col, c => c.Val), out _);

    // ---------------------------------------------------------------- datumi

    [Fact]
    public void ParseDate_AcceptsIsoCroatianAndSlashFormats()
    {
        var expected = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
        foreach (var s in new[] { "2026-09-30", "30.09.2026.", "30.09.2026", "30. 09. 2026.", "30.9.2026.", "30/09/2026", "2026-09-30 14:30:00", " 2026-09-30 " })
            Assert.Equal(expected, ProvenanceRules.ParseDate(s));
    }

    [Fact]
    public void ParseDate_AcceptsExcelSerialNumber()
    {
        Assert.Equal(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc), ProvenanceRules.ParseDate("46295"));
    }

    [Fact]
    public void ParseDate_ReturnsUtcKind_BecauseNpgsqlRejectsOthersForTimestamptz()
    {
        // oba puta: tekst (parsiranje) i Excel serijski broj (FromOADate vraća Kind=Unspecified pa ga treba izričito postaviti)
        Assert.Equal(DateTimeKind.Utc, ProvenanceRules.ParseDate("30.09.2026.")!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, ProvenanceRules.ParseDate("46295")!.Value.Kind);
    }

    [Fact]
    public void ParseDate_RejectsBlankGarbageAndImplausible()
    {
        foreach (var s in new[] { "", "  ", "jučer", "31.02.2026.", "5", "1850-01-01", "2500-01-01" })
            Assert.Null(ProvenanceRules.ParseDate(s));
        Assert.Null(ProvenanceRules.ParseDate(null));
    }

    [Fact]
    public void AsUtc_NormalizesEveryKind_BecauseNpgsqlRejectsUnspecifiedForTimestamptz()
    {
        Assert.Null(ProvenanceRules.AsUtc(null));

        var utc = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(utc, ProvenanceRules.AsUtc(utc));

        // JSON bez zone dolazi kao Unspecified → tumači se kao UTC (isti sat, samo Kind)
        var unspecified = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Unspecified);
        var fromUnspecified = ProvenanceRules.AsUtc(unspecified)!.Value;
        Assert.Equal(DateTimeKind.Utc, fromUnspecified.Kind);
        Assert.Equal(unspecified.Ticks, fromUnspecified.Ticks);

        var local = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);
        var fromLocal = ProvenanceRules.AsUtc(local)!.Value;
        Assert.Equal(DateTimeKind.Utc, fromLocal.Kind);
        Assert.Equal(local.ToUniversalTime(), fromLocal);
    }

    // ---------------------------------------------------------------- mapiranje HR → interno

    [Fact]
    public void Status_MapsCroatianValuesToInternal()
    {
        Assert.Equal("unknown", Parse(("privola_status", "nepoznato")).ConsentStatus);
        Assert.Equal("requested", Parse(("privola_status", "zatraženo")).ConsentStatus);
        Assert.Equal("granted", Parse(("privola_status", "dano")).ConsentStatus);
        Assert.Equal("refused", Parse(("privola_status", "odbijeno")).ConsentStatus);
    }

    [Fact]
    public void Status_IsCaseWhitespaceAndDiacriticInsensitive_AndAcceptsInternalNames()
    {
        Assert.Equal("requested", Parse(("privola_status", "  Zatrazeno ")).ConsentStatus);
        Assert.Equal("requested", Parse(("privola_status", "ZATRAŽENO")).ConsentStatus);
        Assert.Equal("granted", Parse(("privola_status", "granted")).ConsentStatus);
    }

    [Fact]
    public void Status_UnknownValue_WarnsAndIsIgnored()
    {
        var input = Parse(new Dictionary<string, string> { ["privola_status"] = "možda" }, out var warnings);

        Assert.Null(input.ConsentStatus);
        Assert.Contains(warnings, w => w.StartsWith("privola_status") && w.Contains("možda"));
    }

    [Fact]
    public void Refused_AddsOptOutWarning()
    {
        Parse(new Dictionary<string, string> { ["privola_status"] = "odbijeno" }, out var warnings);
        Assert.Contains("privola odbijena — profil skriven (opt-out)", warnings);
    }

    [Fact]
    public void Source_MapsAliases_AndWarnsOnUnknown()
    {
        Assert.Equal("google_maps", Parse(("izvor_podataka", "Google Maps")).DataSource);
        Assert.Equal("instagram", Parse(("izvor_podataka", "IG")).DataSource);
        Assert.Equal("preporuka", Parse(("izvor_podataka", "preporuka")).DataSource);

        var input = Parse(new Dictionary<string, string> { ["izvor_podataka"] = "tiktok" }, out var warnings);
        Assert.Null(input.DataSource);
        Assert.Single(warnings);
    }

    [Fact]
    public void Channel_MapsAliases_AndRejectsClaim()
    {
        Assert.Equal("email", Parse(("privola_kanal", "E-mail")).ConsentChannel);
        Assert.Equal("telefon", Parse(("privola_kanal", "telefon")).ConsentChannel);
        // "claim" postavlja samo sustav pri odobrenju claima — Excel ga ne smije upisati
        var input = Parse(new Dictionary<string, string> { ["privola_kanal"] = "claim" }, out var warnings);
        Assert.Null(input.ConsentChannel);
        Assert.Single(warnings);
    }

    [Fact]
    public void Scope_MapsCroatianValues_CanonicalOrder_NoDuplicates()
    {
        Assert.Equal(new[] { "data", "photos" }, Parse(("privola_opseg", "podaci, slike")).ConsentScope!);
        Assert.Equal(new[] { "data", "photos", "reviews" }, Parse(("privola_opseg", "recenzije; slike; podaci; podaci")).ConsentScope!);
    }

    [Fact]
    public void Scope_UnknownItemsAreSkippedWithWarning_ValidOnesKept()
    {
        var input = Parse(new Dictionary<string, string> { ["privola_opseg"] = "slike, videi" }, out var warnings);

        Assert.Equal(new[] { "photos" }, input.ConsentScope!);
        Assert.Contains(warnings, w => w.Contains("videi"));
    }

    [Fact]
    public void Scope_BlankOrAllInvalid_IsNullSoDatabaseValueStays()
    {
        Assert.Null(Parse(("privola_opseg", "")).ConsentScope);
        Assert.Null(Parse(("privola_opseg", "ništa")).ConsentScope);
    }

    [Fact]
    public void Dates_AreParsed_AndInvalidOnesWarn()
    {
        var input = Parse(new Dictionary<string, string>
        {
            ["datum_prikupljanja"] = "30.09.2026.", ["privola_zatrazena"] = "2026-09-20", ["privola_datum"] = "kad-tad",
        }, out var warnings);

        Assert.Equal(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc), input.DataCollectedAt);
        Assert.Equal(new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), input.ConsentRequestedAt);
        Assert.Null(input.ConsentAt);
        Assert.Contains(warnings, w => w.StartsWith("privola_datum"));
    }

    [Fact]
    public void PlaceId_IsTrimmed_AndWarnsWhenItContainsSpaces_ButIsStillKept()
    {
        Assert.Equal("ChIJabc123", Parse(("google_place_id", "  ChIJabc123 ")).GooglePlaceId);

        var input = Parse(new Dictionary<string, string> { ["google_place_id"] = "ChIJ abc" }, out var warnings);
        Assert.Equal("ChIJ abc", input.GooglePlaceId);
        Assert.Contains(warnings, w => w.StartsWith("google_place_id"));
    }

    [Fact]
    public void BlankRow_GivesAllNull_AndNoWarnings()
    {
        var input = Parse(new Dictionary<string, string>(), out var warnings);

        Assert.Equal(new ProvenanceInput(null, null, null, null, null, null, null, null, null), input);
        Assert.Empty(warnings);
    }

    // ---------------------------------------------------------------- primjena na pružatelja

    private static Vendor Vendor(string claimStatus = "unclaimed") => new()
    {
        Slug = "foto-anic", Name = "Foto Anić", CategorySlug = "foto-i-video", RegionSlug = "dalmacija", ClaimStatus = claimStatus,
    };

    private static readonly ProvenanceInput Blank = new(null, null, null, null, null, null, null, null, null);

    [Fact]
    public void BlankInput_DoesNotWipeExistingValues()
    {
        var v = Vendor();
        v.DataSource = "web"; v.DataCollectedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        v.ConsentStatus = "granted"; v.ConsentChannel = "email"; v.ConsentNote = "ručni unos admina";
        v.ConsentScope = new List<string> { "data" }; v.GooglePlaceId = "ChIJstari";

        var warnings = ProvenanceMerge.Apply(v, Blank);

        Assert.Empty(warnings);
        Assert.Equal("web", v.DataSource);
        Assert.Equal("granted", v.ConsentStatus);
        Assert.Equal("email", v.ConsentChannel);
        Assert.Equal("ručni unos admina", v.ConsentNote);
        Assert.Equal(new[] { "data" }, v.ConsentScope);
        Assert.Equal("ChIJstari", v.GooglePlaceId);
        Assert.NotNull(v.DataCollectedAt);
    }

    [Fact]
    public void ProvidedValues_AreApplied()
    {
        var v = Vendor();
        var input = new ProvenanceInput("instagram", new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), "requested",
            new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), null, "instagram", new List<string> { "data", "photos" }, "poslan DM", "ChIJnovi");

        ProvenanceMerge.Apply(v, input);

        Assert.Equal("instagram", v.DataSource);
        Assert.Equal("requested", v.ConsentStatus);
        Assert.Equal("instagram", v.ConsentChannel);
        Assert.Equal(new[] { "data", "photos" }, v.ConsentScope);
        Assert.Equal("poslan DM", v.ConsentNote);
        Assert.Equal("ChIJnovi", v.GooglePlaceId);
        Assert.Null(v.ConsentAt); // nije u Excelu → ostaje
    }

    [Fact]
    public void ConsentScope_IsCopied_NotSharedByReference()
    {
        var v = Vendor();
        var scope = new List<string> { "data" };
        ProvenanceMerge.Apply(v, Blank with { ConsentScope = scope });
        scope.Add("photos");
        Assert.Equal(new[] { "data" }, v.ConsentScope);
    }

    [Fact]
    public void Refused_SetsOptOut_AndStatus()
    {
        var v = Vendor();
        Assert.False(v.OptOut);

        ProvenanceMerge.Apply(v, Blank with { ConsentStatus = "refused" });

        Assert.True(v.OptOut);
        Assert.Equal("refused", v.ConsentStatus);
    }

    [Fact]
    public void Import_NeverClearsOptOut()
    {
        var v = Vendor();
        v.OptOut = true;

        ProvenanceMerge.Apply(v, Blank with { ConsentStatus = "granted" });
        Assert.True(v.OptOut);

        ProvenanceMerge.Apply(v, Blank);
        Assert.True(v.OptOut);
    }

    [Fact]
    public void ClaimedGrantedViaClaim_IsNotDowngradedToWeakerStatus()
    {
        foreach (var weaker in new[] { "unknown", "requested" })
        {
            var v = Vendor("claimed");
            v.ConsentStatus = "granted"; v.ConsentChannel = "claim"; v.ConsentAt = new DateTime(2026, 9, 20, 10, 30, 0, DateTimeKind.Utc);
            v.ConsentScope = new List<string> { "data", "photos", "reviews" };

            var warnings = ProvenanceMerge.Apply(v, Blank with { ConsentStatus = weaker });

            Assert.Equal("granted", v.ConsentStatus);
            Assert.Equal("claim", v.ConsentChannel);
            Assert.Equal(3, v.ConsentScope.Count);
            Assert.False(v.OptOut);
            Assert.Contains(warnings, w => w.Contains("claim"));
        }
    }

    [Fact]
    public void ClaimedGrantedViaClaim_ConsentBlockIsNotOverwrittenByAnotherGrant()
    {
        var v = Vendor("claimed");
        v.ConsentStatus = "granted"; v.ConsentChannel = "claim"; v.ConsentScope = new List<string> { "data", "photos", "reviews" };

        // Excel kaže "dano" ali preko e-maila, samo za podatke — to bi prebrisalo jači dokaz (claim)
        var warnings = ProvenanceMerge.Apply(v, Blank with { ConsentStatus = "granted", ConsentChannel = "email", ConsentScope = new List<string> { "data" } });

        Assert.Equal("claim", v.ConsentChannel);
        Assert.Equal(3, v.ConsentScope.Count);
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public void ClaimedGrantedViaClaim_SameValuesInExcel_NoWarning()
    {
        var v = Vendor("claimed");
        v.ConsentStatus = "granted"; v.ConsentChannel = "claim";

        Assert.Empty(ProvenanceMerge.Apply(v, Blank with { ConsentStatus = "granted", ConsentChannel = "claim" }));
    }

    [Fact]
    public void ClaimedGrantedViaClaim_OnlyRefusedOverrides_AndHidesProfile()
    {
        var v = Vendor("claimed");
        v.ConsentStatus = "granted"; v.ConsentChannel = "claim";

        ProvenanceMerge.Apply(v, Blank with { ConsentStatus = "refused" });

        Assert.Equal("refused", v.ConsentStatus);
        Assert.True(v.OptOut);
    }

    [Fact]
    public void ClaimedGrantedViaClaim_NonConsentFieldsAreStillApplied()
    {
        var v = Vendor("claimed");
        v.ConsentStatus = "granted"; v.ConsentChannel = "claim";

        ProvenanceMerge.Apply(v, Blank with { DataSource = "web", GooglePlaceId = "ChIJxyz", ConsentNote = "dopuna" });

        Assert.Equal("web", v.DataSource);
        Assert.Equal("ChIJxyz", v.GooglePlaceId);
        Assert.Equal("dopuna", v.ConsentNote);
    }

    [Fact]
    public void UnclaimedVendorWithGrantedConsent_CanBeChangedFromExcel()
    {
        var v = Vendor("unclaimed");
        v.ConsentStatus = "granted"; v.ConsentChannel = "email";

        ProvenanceMerge.Apply(v, Blank with { ConsentStatus = "requested" });

        Assert.Equal("requested", v.ConsentStatus);
    }

    // ---------------------------------------------------------------- privola putem claima

    [Fact]
    public void GrantViaClaim_SetsGrantedClaimChannelTimeAndFullScope()
    {
        var v = Vendor("claimed");
        var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        var granted = ConsentRules.GrantViaClaim(v, now);

        Assert.True(granted);
        Assert.Equal("granted", v.ConsentStatus);
        Assert.Equal("claim", v.ConsentChannel);
        Assert.Equal(now, v.ConsentAt);
        Assert.Equal(new[] { "data", "photos", "reviews" }, v.ConsentScope);
    }

    [Fact]
    public void GrantViaClaim_OverridesRequestedOrUnknown()
    {
        foreach (var before in new[] { "unknown", "requested" })
        {
            var v = Vendor("claimed");
            v.ConsentStatus = before; v.ConsentChannel = "email";
            Assert.True(ConsentRules.GrantViaClaim(v, DateTime.UtcNow));
            Assert.Equal("granted", v.ConsentStatus);
            Assert.Equal("claim", v.ConsentChannel);
        }
    }

    [Fact]
    public void GrantViaClaim_DoesNotSilentlyUndoARefusal()
    {
        var v = Vendor("claimed");
        v.ConsentStatus = "refused"; v.ConsentChannel = "email";

        var granted = ConsentRules.GrantViaClaim(v, DateTime.UtcNow);

        Assert.False(granted);
        Assert.Equal("refused", v.ConsentStatus);
        Assert.Equal("email", v.ConsentChannel);
        Assert.Null(v.ConsentAt);
    }

    [Fact]
    public void TemplateAndRules_ShareTheSameAllowedValues()
    {
        // čuvar: padajuće liste u scripts/make-template.py i ovdje moraju ostati usklađene
        Assert.Equal(new[] { "google_maps", "web", "instagram", "facebook", "partner", "preporuka", "drugo" }, ProvenanceRules.DataSources);
        Assert.Equal(new[] { "email", "instagram", "facebook", "telefon", "osobno" }, ProvenanceRules.ConsentChannelsFromExcel);
        Assert.Equal(new[] { "data", "photos", "reviews" }, ProvenanceRules.ConsentScopes);
    }
}
