using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Wediplan.Api.Domain;
using Wediplan.Api.Infrastructure.Audit;
using Xunit;

namespace Wediplan.Api.Tests;

/// <summary>
/// §Zadatak 14 — čista logika dnevnika (<see cref="AuditEntryBuilder"/> + <see cref="AuditRules"/>), bez EF-a i baze:
/// što ulazi u zapis, što se maskira, kad se ništa ne zapisuje i koja se akcija bira.
/// </summary>
public class AuditEntryBuilderTests
{
    private static JsonElement Parse(string? json) => JsonDocument.Parse(json!).RootElement;
    private static PropChange Ch(string name, object? o, object? c) => new(name, o, c);

    [Fact]
    public void Modified_WritesOldAndNewValue()
    {
        var log = AuditEntryBuilder.ForModified("vendor", "abc", null, new[] { Ch("PriceFrom", 800, 950) });

        Assert.NotNull(log);
        Assert.Equal("update", log!.Action);
        Assert.Equal("vendor", log.EntityType);
        Assert.Equal("abc", log.EntityId);
        Assert.Null(log.Note);
        var price = Parse(log.Changes).GetProperty("PriceFrom");
        Assert.Equal(800, price.GetProperty("old").GetInt32());
        Assert.Equal(950, price.GetProperty("new").GetInt32());
    }

    [Fact]
    public void ContactFields_AreMasked_NoValuesInJson()
    {
        var log = AuditEntryBuilder.ForModified("vendor", "abc", null, new[]
        {
            Ch("Phone", "021111222", "021999888"),
            Ch("Email", "stari@primjer.hr", "novi@primjer.hr"),
            Ch("SocialInstagram", "@stari", "@novi"),
        });

        Assert.NotNull(log);
        var json = log!.Changes!;
        Assert.True(Parse(json).GetProperty("Phone").GetProperty("changed").GetBoolean());
        foreach (var secret in new[] { "021111222", "021999888", "stari@primjer.hr", "novi@primjer.hr", "@stari", "@novi" })
            Assert.DoesNotContain(secret, json);
    }

    [Fact]
    public void UpdatedAtOnly_WritesNothing()
    {
        var log = AuditEntryBuilder.ForModified("vendor", "abc", null,
            new[] { Ch("UpdatedAt", DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow) });
        Assert.Null(log);
    }

    [Fact]
    public void ListReplacedWithSameContent_WritesNothing()
    {
        var log = AuditEntryBuilder.ForModified("vendor", "abc", null, new[]
        {
            Ch("Services", new List<string> { "Vjenčanja", "Zaruke" }, new List<string> { "Vjenčanja", "Zaruke" }),
        });
        Assert.Null(log);
    }

    [Fact]
    public void ListChanged_IsWrittenAsJsonArrays()
    {
        var log = AuditEntryBuilder.ForModified("vendor", "abc", null, new[]
        {
            Ch("Services", new List<string> { "Vjenčanja" }, new List<string> { "Vjenčanja", "Zaruke" }),
        });

        var services = Parse(log!.Changes).GetProperty("Services");
        Assert.Equal(1, services.GetProperty("old").GetArrayLength());
        Assert.Equal("Zaruke", services.GetProperty("new")[1].GetString());
    }

    [Fact]
    public void OptOutChange_UsesOptoutActions()
    {
        var on = AuditEntryBuilder.ForModified("vendor", "abc", null, new[] { Ch("OptOut", false, true) });
        var off = AuditEntryBuilder.ForModified("vendor", "abc", null, new[] { Ch("OptOut", true, false) });

        Assert.Equal("optout", on!.Action);
        Assert.Equal("optout_restored", off!.Action);
    }

    [Fact]
    public void OptOut_OnOtherEntityTypes_IsJustUpdate()
    {
        // "OptOut" ima posebnu akciju samo na pružatelju.
        var log = AuditEntryBuilder.ForModified("claim", "abc", Guid.NewGuid().ToString(), new[] { Ch("OptOut", false, true) });
        Assert.Equal("update", log!.Action);
    }

    [Fact]
    public void ChildEntities_CarryVendorIdInNote()
    {
        var vid = Guid.NewGuid();
        var log = AuditEntryBuilder.ForModified("user_review", "r1", vid.ToString(), new[] { Ch("Status", "pending", "published") });

        Assert.Equal(AuditEntryBuilder.VendorNote(vid), log!.Note);
        Assert.Equal($"vendorId:{vid}", log.Note);
    }

    [Fact]
    public void ReviewText_IsMasked()
    {
        var log = AuditEntryBuilder.ForModified("user_review", "r1", Guid.NewGuid().ToString(),
            new[] { Ch("Text", "Super fotograf, preporučam", "Uređeni tekst recenzije") });

        Assert.DoesNotContain("fotograf", log!.Changes!);
        Assert.DoesNotContain("Uređeni", log.Changes!);
        Assert.True(Parse(log.Changes).GetProperty("Text").GetProperty("changed").GetBoolean());
    }

    [Fact]
    public void Added_WritesNewValues_MasksContacts_SkipsNullsKeysAndEmptyLists()
    {
        var vid = Guid.NewGuid();
        var log = AuditEntryBuilder.ForAdded("vendor", vid.ToString(), null, new[]
        {
            new PropValue("Id", vid),
            new PropValue("Name", "Foto Anić"),
            new PropValue("Phone", "021111222"),
            new PropValue("CoverageNote", null),
            new PropValue("Services", new List<string>()),
            new PropValue("StyleTags", new List<string> { "reportažni" }),
            new PropValue("UpdatedAt", DateTime.UtcNow),
        });

        Assert.Equal("create", log.Action);
        var root = Parse(log.Changes);
        Assert.Equal("Foto Anić", root.GetProperty("Name").GetString()); // hrvatski znakovi nisu \u-escapani
        Assert.Contains("Foto Anić", log.Changes!);
        Assert.True(root.GetProperty("Phone").GetProperty("set").GetBoolean());
        Assert.DoesNotContain("021111222", log.Changes!);
        Assert.False(root.TryGetProperty("Id", out _));
        Assert.False(root.TryGetProperty("CoverageNote", out _));
        Assert.False(root.TryGetProperty("Services", out _));
        Assert.False(root.TryGetProperty("UpdatedAt", out _));
        Assert.Equal("reportažni", root.GetProperty("StyleTags")[0].GetString());
    }

    [Fact]
    public void Added_ChildEntity_DoesNotRepeatVendorIdInChanges()
    {
        var vid = Guid.NewGuid();
        var log = AuditEntryBuilder.ForAdded("vendor_photo", "p1", vid.ToString(), new[]
        {
            new PropValue("VendorId", vid), new PropValue("StorageKey", "/uploads/x.webp"),
        });

        Assert.Equal($"vendorId:{vid}", log.Note);
        Assert.DoesNotContain(vid.ToString(), log.Changes!);
    }

    [Fact]
    public void Deleted_Photo_KeepsOnlyStorageKey_AndVendorNote()
    {
        var vid = Guid.NewGuid();
        var log = AuditEntryBuilder.ForDeleted("vendor_photo", "p1", vid.ToString(), new[]
        {
            new PropValue("StorageKey", "/uploads/x.webp"), new PropValue("SortOrder", 3), new PropValue("ModerationNote", "tajna"),
        });

        Assert.Equal("delete", log.Action);
        Assert.Equal($"vendorId:{vid}", log.Note);
        var root = Parse(log.Changes);
        Assert.Equal("/uploads/x.webp", root.GetProperty("StorageKey").GetString());
        Assert.False(root.TryGetProperty("SortOrder", out _));
        Assert.DoesNotContain("tajna", log.Changes!);
    }

    [Fact]
    public void Deleted_ReviewHasNoChanges_ButKeepsVendorNote()
    {
        var vid = Guid.NewGuid();
        var log = AuditEntryBuilder.ForDeleted("imported_review", "r1", vid.ToString(),
            new[] { new PropValue("Author", "Ana"), new PropValue("Text", "Super") });

        Assert.Null(log.Changes);
        Assert.Equal($"vendorId:{vid}", log.Note);
    }

    [Fact]
    public void LongText_IsTruncated()
    {
        var longText = new string('x', 5000);
        var log = AuditEntryBuilder.ForModified("vendor", "abc", null, new[] { Ch("About", "kratko", longText) });

        var written = Parse(log!.Changes).GetProperty("About").GetProperty("new").GetString()!;
        Assert.Equal(AuditEntryBuilder.MaxStringLength + 1, written.Length); // MaxStringLength znakova + "…"
        Assert.True(written.EndsWith("…"));
    }

    [Fact]
    public void TrackedEntities_AreExactlyTheFiveFromThePlan()
    {
        Assert.Equal("vendor", AuditRules.EntityTypeOf(typeof(Vendor)));
        Assert.Equal("vendor_photo", AuditRules.EntityTypeOf(typeof(VendorPhoto)));
        Assert.Equal("imported_review", AuditRules.EntityTypeOf(typeof(ImportedReview)));
        Assert.Equal("user_review", AuditRules.EntityTypeOf(typeof(UserReview)));
        Assert.Equal("claim", AuditRules.EntityTypeOf(typeof(Claim)));

        // NE prate se (plan): draft, osobni podaci korisnika, analitika, tokeni i sam dnevnik (nema rekurzije)
        foreach (var t in new[] { typeof(VendorDraft), typeof(Favorite), typeof(BudgetPlan), typeof(Event),
                                  typeof(DailyStat), typeof(MagicLink), typeof(EmailVerificationToken),
                                  typeof(ClaimVerificationToken), typeof(AuditLog), typeof(AppUser) })
            Assert.Null(AuditRules.EntityTypeOf(t));
    }
}
