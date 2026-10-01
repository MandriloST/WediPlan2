using System;
using System.Collections.Generic;
using System.Linq;
using Wediplan.Api.Domain;
using Wediplan.Api.Import;
using Xunit;

namespace Wediplan.Api.Tests;

/// <summary>
/// §Zadatak 16 — <see cref="ImportedReviewMerge.Apply"/>: ponovni Excel uvoz NE smije izgubiti status provjere
/// uvezenih recenzija, a ne smije ni stvarati/brisati nepromijenjene retke. Čiste jedinice, bez baze i Excela.
/// </summary>
public class ImportedReviewMergeTests
{
    private const string Slug = "foto-anic";
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Recenzija kakvu daje parsiranje Excela (bez ključa, "unverified").</summary>
    private static ImportedReview FromExcel(string text = "Odličan fotograf", int rating = 5, string author = "Ana", string source = "Google", int year = 2025) =>
        new() { Author = author, Rating = rating, Text = text, Source = source, Year = year };

    /// <summary>Recenzija kakva je već u bazi (s ključem i zadanim statusom provjere).</summary>
    private static ImportedReview InDb(string text = "Odličan fotograf", int rating = 5, string status = "unverified",
        bool withKey = true, string author = "Ana", string source = "Google", int year = 2025)
    {
        var r = FromExcel(text, rating, author, source, year);
        r.VerificationStatus = status;
        if (withKey) r.ExternalKey = ImportRules.ReviewKey(Slug, author, text, source, year);
        return r;
    }

    [Fact]
    public void SameReview_IsKept_AndVerifiedStatusPreserved()
    {
        var verified = InDb(status: "verified");
        verified.VerifiedByUserId = Guid.NewGuid();
        verified.EvidenceNote = "screenshot u Driveu";

        var res = ImportedReviewMerge.Apply(Slug, new[] { verified }, new[] { FromExcel() }, Now);

        Assert.Empty(res.ToAdd);
        Assert.Empty(res.ToRemove);
        Assert.Equal(1, res.Kept);
        Assert.Equal("verified", verified.VerificationStatus);
        Assert.Equal("screenshot u Driveu", verified.EvidenceNote);
        Assert.NotNull(verified.VerifiedByUserId);
    }

    [Fact]
    public void NewReview_IsAdded_WithKeyCreatedAtAndUnverified()
    {
        var incoming = FromExcel("Nova recenzija");

        var res = ImportedReviewMerge.Apply(Slug, new[] { InDb() }, new[] { FromExcel(), incoming }, Now);

        var added = Assert.Single(res.ToAdd);
        Assert.Same(incoming, added);
        Assert.Equal(ImportRules.ReviewKey(Slug, "Ana", "Nova recenzija", "Google", 2025), added.ExternalKey);
        Assert.Equal(Now, added.CreatedAt);
        Assert.Equal("unverified", added.VerificationStatus);
        Assert.Equal(1, res.Kept);
    }

    [Fact]
    public void ReviewRemovedFromExcel_IsMarkedForRemoval()
    {
        var gone = InDb("Ova je maknuta iz Excela");
        var stays = InDb();

        var res = ImportedReviewMerge.Apply(Slug, new[] { stays, gone }, new[] { FromExcel() }, Now);

        Assert.Same(gone, Assert.Single(res.ToRemove));
        Assert.Equal(1, res.Kept);
    }

    [Fact]
    public void EmptyExcel_RemovesEverything()
    {
        var res = ImportedReviewMerge.Apply(Slug, new[] { InDb(), InDb("druga") }, Array.Empty<ImportedReview>(), Now);
        Assert.Equal(2, res.ToRemove.Count);
        Assert.Empty(res.ToAdd);
    }

    [Fact]
    public void LegacyRowWithoutKey_IsMatched_GetsKeyBackfilled_AndStatusPreserved()
    {
        var legacy = InDb(status: "verified", withKey: false);
        Assert.Null(legacy.ExternalKey);

        var res = ImportedReviewMerge.Apply(Slug, new[] { legacy }, new[] { FromExcel() }, Now);

        Assert.Empty(res.ToAdd);
        Assert.Empty(res.ToRemove);
        Assert.Equal(ImportRules.ReviewKey(Slug, "Ana", "Odličan fotograf", "Google", 2025), legacy.ExternalKey);
        Assert.Equal("verified", legacy.VerificationStatus);
    }

    [Fact]
    public void LegacyRowNotInExcel_IsRemoved_AsBefore()
    {
        var legacy = InDb("stara recenzija", withKey: false);
        var res = ImportedReviewMerge.Apply(Slug, new[] { legacy }, new[] { FromExcel("potpuno druga") }, Now);

        Assert.Same(legacy, Assert.Single(res.ToRemove));
        Assert.Single(res.ToAdd);
    }

    [Fact]
    public void RatingChange_UpdatesRatingAndUpdatedAt_KeepsStatus()
    {
        var existing = InDb(rating: 4, status: "verified");

        var res = ImportedReviewMerge.Apply(Slug, new[] { existing }, new[] { FromExcel(rating: 5) }, Now);

        Assert.Equal(5, existing.Rating);
        Assert.Equal(Now, existing.UpdatedAt);
        Assert.Equal("verified", existing.VerificationStatus);
        Assert.Equal(1, res.RatingUpdated);
        Assert.Empty(res.ToAdd);
        Assert.Empty(res.ToRemove);
    }

    [Fact]
    public void UnchangedRating_DoesNotTouchUpdatedAt()
    {
        var existing = InDb(rating: 5);
        var res = ImportedReviewMerge.Apply(Slug, new[] { existing }, new[] { FromExcel(rating: 5) }, Now);

        Assert.Null(existing.UpdatedAt);
        Assert.Equal(0, res.RatingUpdated);
    }

    [Fact]
    public void ChangedText_IsANewReview_OldOneIsRemoved()
    {
        var old = InDb("Prvotni tekst", status: "verified");

        var res = ImportedReviewMerge.Apply(Slug, new[] { old }, new[] { FromExcel("Izmijenjeni tekst") }, Now);

        Assert.Same(old, Assert.Single(res.ToRemove));
        var added = Assert.Single(res.ToAdd);
        Assert.Equal("unverified", added.VerificationStatus); // admin je provjerio KONKRETAN tekst; novi tekst se provjerava ponovno
    }

    [Fact]
    public void RejectedReviewStillInExcel_StaysRejected()
    {
        var rejected = InDb(status: "rejected");
        var res = ImportedReviewMerge.Apply(Slug, new[] { rejected }, new[] { FromExcel() }, Now);

        Assert.Empty(res.ToAdd);
        Assert.Empty(res.ToRemove);
        Assert.Equal("rejected", rejected.VerificationStatus);
    }

    [Fact]
    public void DuplicatesInExcel_AreCollapsed_AndCounted()
    {
        var res = ImportedReviewMerge.Apply(Slug, Array.Empty<ImportedReview>(),
            new[] { FromExcel(), FromExcel(), FromExcel("druga") }, Now);

        Assert.Equal(2, res.ToAdd.Count);
        Assert.Equal(1, res.DuplicatesInExcel);
        Assert.Equal(2, res.ToAdd.Select(r => r.ExternalKey).Distinct().Count()); // jedinstveni indeks (vendor_id, external_key)
    }

    [Fact]
    public void DuplicateExistingRows_KeepTheMostAdvancedStatus()
    {
        var plain = InDb(status: "unverified");
        var verified = InDb(status: "verified");

        var res = ImportedReviewMerge.Apply(Slug, new[] { plain, verified }, new[] { FromExcel() }, Now);

        Assert.Same(plain, Assert.Single(res.ToRemove)); // admin rad (verified) ima prednost
        Assert.Equal(1, res.Kept);
    }

    [Fact]
    public void WhitespaceAndCaseDifferencesInExcel_DoNotCreateANewReview()
    {
        var existing = InDb("Odličan fotograf", status: "verified");

        var res = ImportedReviewMerge.Apply(Slug, new[] { existing },
            new[] { FromExcel("  odlican   FOTOGRAF ", author: " ANA ", source: "google") }, Now);

        Assert.Empty(res.ToAdd);
        Assert.Empty(res.ToRemove);
        Assert.Equal("verified", existing.VerificationStatus);
    }

    [Fact]
    public void SecondImportOfSameExcel_IsANoOp()
    {
        // Prvi uvoz: sve nove. Drugi uvoz istog Excela: ništa se ne dodaje, ne briše ni ne ažurira.
        var excel1 = new[] { FromExcel("a"), FromExcel("b", rating: 3) };
        var first = ImportedReviewMerge.Apply(Slug, Array.Empty<ImportedReview>(), excel1, Now);
        var inDb = first.ToAdd.ToList();

        var excel2 = new[] { FromExcel("a"), FromExcel("b", rating: 3) };
        var second = ImportedReviewMerge.Apply(Slug, inDb, excel2, Now.AddDays(1));

        Assert.Equal(2, first.ToAdd.Count);
        Assert.Empty(second.ToAdd);
        Assert.Empty(second.ToRemove);
        Assert.Equal(2, second.Kept);
        Assert.Equal(0, second.RatingUpdated);
    }
}
