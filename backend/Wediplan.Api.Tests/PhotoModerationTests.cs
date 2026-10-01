using System;
using System.Collections.Generic;
using System.Linq;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;
using Wediplan.Api.Services;
using Xunit;

namespace Wediplan.Api.Tests;

/// <summary>
/// §Zadatak 15 — post-moderacija fotografija: prijelazi statusa i evidencija admina (<see cref="PhotoModeration"/>),
/// te javni prikaz bez sakrivenih slika (<see cref="VendorMapper"/>). Čiste jedinice, bez baze.
/// </summary>
public class PhotoModerationTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static VendorPhoto Photo(string status = "unreviewed", int sort = 0, string key = "/uploads/a.webp") =>
        new() { VendorId = Guid.NewGuid(), StorageKey = key, SortOrder = sort, ModerationStatus = status };

    // ---------------------------------------------------------------- prijelazi

    [Fact]
    public void AllowedTransitions_AreExactlyTheFourFromThePlan()
    {
        var statuses = new[] { "unreviewed", "approved", "flagged" };
        var allowed = (from f in statuses from t in statuses where PhotoModeration.CanTransition(f, t) select $"{f}->{t}").ToList();

        Assert.Equal(
            new[] { "unreviewed->approved", "unreviewed->flagged", "approved->flagged", "flagged->approved" }.OrderBy(x => x),
            allowed.OrderBy(x => x));
    }

    [Fact]
    public void CanNeverGoBackToUnreviewed()
    {
        Assert.False(PhotoModeration.CanTransition("approved", "unreviewed"));
        Assert.False(PhotoModeration.CanTransition("flagged", "unreviewed"));
    }

    [Fact]
    public void UnknownStatuses_AreNotValid()
    {
        Assert.True(PhotoModeration.IsValidStatus("flagged"));
        Assert.False(PhotoModeration.IsValidStatus("pending"));
        Assert.False(PhotoModeration.IsValidStatus(null));
        Assert.False(PhotoModeration.CanTransition("nepoznato", "approved"));
    }

    // ---------------------------------------------------------------- primjena odluke

    [Fact]
    public void Approve_SetsStatusReviewerAndTime()
    {
        var photo = Photo();
        var admin = Guid.NewGuid();

        var ok = PhotoModeration.TryApply(photo, "approved", admin, null, Now);

        Assert.True(ok);
        Assert.Equal("approved", photo.ModerationStatus);
        Assert.Equal(admin, photo.ReviewedByUserId);
        Assert.Equal(Now, photo.ReviewedAt);
        Assert.Null(photo.ModerationNote);
    }

    [Fact]
    public void Flag_StoresNote_AndRecordsReviewer()
    {
        var photo = Photo();
        var admin = Guid.NewGuid();

        var ok = PhotoModeration.TryApply(photo, "flagged", admin, "Fotografija nije vaša", Now);

        Assert.True(ok);
        Assert.Equal("flagged", photo.ModerationStatus);
        Assert.Equal("Fotografija nije vaša", photo.ModerationNote);
        Assert.Equal(admin, photo.ReviewedByUserId);
    }

    [Fact]
    public void Unflag_BringsBackToApproved_AndClearsTheNote()
    {
        var photo = Photo(status: "flagged");
        photo.ModerationNote = "stari razlog";

        var ok = PhotoModeration.TryApply(photo, "approved", Guid.NewGuid(), null, Now);

        Assert.True(ok);
        Assert.Equal("approved", photo.ModerationStatus);
        Assert.Null(photo.ModerationNote); // razlog opisuje skrivanje; kad slika opet postane javna, više ne vrijedi
    }

    [Fact]
    public void InvalidTransition_ChangesNothing()
    {
        var photo = Photo(status: "approved");
        var before = Guid.NewGuid();
        photo.ReviewedByUserId = before;

        var ok = PhotoModeration.TryApply(photo, "approved", Guid.NewGuid(), null, Now);

        Assert.False(ok);
        Assert.Equal("approved", photo.ModerationStatus);
        Assert.Equal(before, photo.ReviewedByUserId); // evidencija prethodne odluke nepromijenjena
        Assert.Null(photo.ReviewedAt);
    }

    [Fact]
    public void FlaggedCannotBeFlaggedAgain()
    {
        var photo = Photo(status: "flagged");
        Assert.False(PhotoModeration.TryApply(photo, "flagged", Guid.NewGuid(), "opet", Now));
    }

    // ---------------------------------------------------------------- javni prikaz

    [Fact]
    public void IsPublic_OnlyFlaggedIsHidden()
    {
        Assert.True(PhotoModeration.IsPublic(Photo("unreviewed"))); // post-moderacija: slika je javna ODMAH
        Assert.True(PhotoModeration.IsPublic(Photo("approved")));
        Assert.False(PhotoModeration.IsPublic(Photo("flagged")));
    }

    private static Vendor VendorWith(params VendorPhoto[] photos) => new()
    {
        Slug = "foto-anic", Name = "Foto Anić", CategorySlug = "foto-i-video", RegionSlug = "dalmacija",
        Photos = photos.ToList(),
    };

    [Fact]
    public void PublicDto_ExcludesFlaggedPhotos()
    {
        var v = VendorWith(Photo("unreviewed", 0, "/a.webp"), Photo("flagged", 1, "/b.webp"), Photo("approved", 2, "/c.webp"));

        var dto = VendorMapper.ToDto(v);

        Assert.Equal(new[] { "/a.webp", "/c.webp" }, dto.Photos!);
    }

    [Fact]
    public void WhenCoverIsFlagged_NextPhotoBySortOrderBecomesPublicCover()
    {
        // javno je naslovna = prva po SortOrder; kad je ona sakrivena, automatski se vidi sljedeća
        var v = VendorWith(Photo("flagged", 0, "/cover.webp"), Photo("approved", 1, "/druga.webp"), Photo("unreviewed", 2, "/treca.webp"));

        var dto = VendorMapper.ToDto(v);

        Assert.Equal("/druga.webp", dto.Photos![0]);
    }

    [Fact]
    public void AllPhotosFlagged_PublicDtoHasNoPhotos()
    {
        var v = VendorWith(Photo("flagged", 0, "/a.webp"), Photo("flagged", 1, "/b.webp"));
        Assert.Null(VendorMapper.ToDto(v).Photos);
    }

    [Fact]
    public void NoPhotosAtAll_PublicDtoHasNoPhotos()
    {
        Assert.Null(VendorMapper.ToDto(VendorWith()).Photos);
    }

    [Fact]
    public void PublicDto_KeepsSortOrder()
    {
        var v = VendorWith(Photo("approved", 5, "/z.webp"), Photo("unreviewed", 1, "/a.webp"), Photo("approved", 3, "/m.webp"));
        Assert.Equal(new[] { "/a.webp", "/m.webp", "/z.webp" }, VendorMapper.ToDto(v).Photos!);
    }
}
