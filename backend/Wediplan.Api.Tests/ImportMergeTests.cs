using System.Collections.Generic;
using System.Linq;
using Wediplan.Api.Domain;
using Wediplan.Api.Import;
using Xunit;

namespace Wediplan.Api.Tests;

/// <summary>
/// §Zadatak 10 — <see cref="ImportMerge.Apply"/>: re-import ne smije prepisati partnerska polja
/// (About, Services, Price*, StyleTags) na preuzetim profilima. Čiste jedinice, bez baze i Excela.
/// </summary>
public class ImportMergeTests
{
    /// <summary>Profil kakav je u bazi nakon što ga je partner preuredio.</summary>
    private static Vendor InDb(string claimStatus, Guid? owner = null) => new()
    {
        Slug = "foto-anic",
        Name = "Foto Anić",
        CategorySlug = "foto-i-video",
        RegionSlug = "dalmacija",
        City = "Split",
        Lat = 43.50, Lng = 16.44, LocationPrecision = "city",
        Phone = "021 111 222",
        About = "Opis koji je uredio partner",
        Services = new List<string> { "Vjenčanja", "Zaruke" },
        StyleTags = new List<string> { "reportažni" },
        PriceKind = "from", PriceFrom = 950, PriceTo = 1500,
        ClaimStatus = claimStatus,
        OwnerUserId = owner,
    };

    /// <summary>Isti pružatelj kako ga opisuje Excel (stari opis/cijene, nova lokacija i telefon).</summary>
    private static Vendor FromExcel() => new()
    {
        Slug = "foto-anic",
        Name = "Foto Anić",
        CategorySlug = "foto-i-video",
        RegionSlug = "dalmacija",
        City = "Split",
        Lat = 43.5081, Lng = 16.4402, LocationPrecision = "exact",
        Phone = "021 999 888",
        About = "Stari opis iz Excela",
        Services = new List<string> { "Vjenčanja" },
        StyleTags = new List<string> { "reportažni", "dokumentarni" },
        PriceKind = "from", PriceFrom = 800, PriceTo = 1200,
        ClaimStatus = "unclaimed", // importer uvijek gradi "unclaimed"; smije se gledati samo existing.ClaimStatus
    };

    [Fact]
    public void Unclaimed_CopiesPartnerFields()
    {
        var existing = InDb("unclaimed");

        var skipped = ImportMerge.Apply(existing, FromExcel());

        Assert.Empty(skipped);
        Assert.Equal("Stari opis iz Excela", existing.About);
        Assert.Equal(new[] { "Vjenčanja" }, existing.Services);
        Assert.Equal(new[] { "reportažni", "dokumentarni" }, existing.StyleTags);
        Assert.Equal(800, existing.PriceFrom);
        Assert.Equal(1200, existing.PriceTo);
    }

    [Fact]
    public void Claimed_KeepsPartnerFields_AndReportsOnlyChangedOnes()
    {
        var existing = InDb("claimed", Guid.NewGuid());
        var excel = FromExcel();
        // Stil u Excelu je IDENTIČAN partnerovom → ne smije se prijaviti kao preskočen.
        excel.StyleTags = new List<string> { "reportažni" };

        var skipped = ImportMerge.Apply(existing, excel);

        // partnerske vrijednosti netaknute
        Assert.Equal("Opis koji je uredio partner", existing.About);
        Assert.Equal(new[] { "Vjenčanja", "Zaruke" }, existing.Services);
        Assert.Equal(new[] { "reportažni" }, existing.StyleTags);
        Assert.Equal("from", existing.PriceKind);
        Assert.Equal(950, existing.PriceFrom);
        Assert.Equal(1500, existing.PriceTo);

        // prijavljena samo polja u kojima se Excel stvarno razlikuje
        Assert.Equal(new[] { "About", "Services", "Price" }, skipped.ToArray());
    }

    [Fact]
    public void Claimed_NothingDiffers_ReportsNothing()
    {
        var existing = InDb("claimed", Guid.NewGuid());
        var excel = FromExcel();
        excel.About = existing.About;
        excel.Services = existing.Services.ToList();
        excel.StyleTags = existing.StyleTags.ToList();
        excel.PriceKind = existing.PriceKind;
        excel.PriceFrom = existing.PriceFrom;
        excel.PriceTo = existing.PriceTo;

        Assert.Empty(ImportMerge.Apply(existing, excel));
    }

    [Fact]
    public void Claimed_StillUpdatesWediplanFields()
    {
        var existing = InDb("claimed", Guid.NewGuid());

        ImportMerge.Apply(existing, FromExcel());

        // polja kojima upravlja Wediplan idu iz Excela i na preuzetom profilu
        Assert.Equal("021 999 888", existing.Phone);
        Assert.Equal(43.5081, existing.Lat!.Value);
        Assert.Equal(16.4402, existing.Lng!.Value);
        Assert.Equal("exact", existing.LocationPrecision);
    }

    [Fact]
    public void Claimed_WithDeletedOwner_StillProtected()
    {
        // Vlasnik je obrisao račun: OwnerUserId = null, ali ClaimStatus ostaje "claimed" (AccountController).
        var existing = InDb("claimed", owner: null);

        var skipped = ImportMerge.Apply(existing, FromExcel());

        Assert.Equal("Opis koji je uredio partner", existing.About);
        Assert.NotEmpty(skipped);
    }

    [Fact]
    public void Apply_DoesNotTouchClaimOwnershipOrVisibility()
    {
        var owner = Guid.NewGuid();
        var existing = InDb("claimed", owner);
        existing.OptOut = true;
        existing.IsPublished = false;

        ImportMerge.Apply(existing, FromExcel());

        Assert.Equal("claimed", existing.ClaimStatus);
        Assert.Equal(owner, existing.OwnerUserId);
        Assert.True(existing.OptOut);
        Assert.False(existing.IsPublished);
    }

    [Fact]
    public void PartnerManagedFields_MatchWhatApplyActuallyProtects()
    {
        // Čuvar od "zaboravio sam dodati novo polje": svako polje iz konstante mora biti
        // prijavljivo kad se razlikuje. (Ako netko doda polje u ApplyToVendor, mora ga dodati ovdje i u Apply.)
        var existing = InDb("claimed", Guid.NewGuid());
        var excel = FromExcel();

        var skipped = ImportMerge.Apply(existing, excel);

        Assert.Equal(ImportMerge.PartnerManagedFields.OrderBy(x => x), skipped.OrderBy(x => x));
    }
}
