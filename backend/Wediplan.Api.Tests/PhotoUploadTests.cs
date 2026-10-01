using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wediplan.Api.Contracts;
using Wediplan.Api.Controllers;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;
using Wediplan.Api.Media;
using Xunit;

namespace Wediplan.Api.Tests;

/// <summary>
/// §Zadatak 15 — <see cref="PhotosController.Upload"/> nad EF InMemory bazom (isti obrazac kao <see cref="ReviewsControllerTests"/>):
/// bez potvrde prava na fotografiju nema uploada; s potvrdom je slika javna ODMAH (post-moderacija) uz evidenciju.
/// </summary>
public class PhotoUploadTests
{
    /// <summary>Valjan PNG 4×4 (sićušan), da stvarni <see cref="ImagePipeline"/> može obraditi upload.</summary>
    private static readonly byte[] TinyPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAAEElEQVR4nGM4YSMHRwzEcQDiIxIhbVeJpgAAAABJRU5ErkJggg==");

    private sealed class FakeStorage : IPhotoStorage
    {
        public System.Collections.Generic.List<string> Saved { get; } = new();
        public Task<string> SaveAsync(string objectKey, byte[] bytes, string contentType, CancellationToken ct = default)
        { Saved.Add(objectKey); return Task.FromResult("/uploads/" + objectKey); }
        public Task DeleteAsync(string publicUrl, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static (AppDbContext Db, UserManager<AppUser> Users) BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<AppUser>(o => o.User.RequireUniqueEmail = true)
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        var scope = services.BuildServiceProvider().CreateScope();
        return (scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>());
    }

    private static PhotosController ControllerAs(AppDbContext db, UserManager<AppUser> users, FakeStorage storage, Guid userId)
    {
        var opt = Options.Create(new StorageOptions());
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "TestAuth"));
        return new PhotosController(db, users, storage, new ImagePipeline(opt), opt)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } },
        };
    }

    private static IFormFile PngFile() =>
        new FormFile(new MemoryStream(TinyPng), 0, TinyPng.Length, "file", "slika.png")
        { Headers = new HeaderDictionary(), ContentType = "image/png" };

    private static async Task<(AppDbContext Db, AppUser User, Vendor Vendor, FakeStorage Storage, PhotosController Controller)> OwnedSetup()
    {
        var (db, users) = BuildServices();
        var user = new AppUser { UserName = "vlasnik@primjer.hr", Email = "vlasnik@primjer.hr", EmailConfirmed = true };
        var created = await users.CreateAsync(user);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));

        var vendor = new Vendor
        {
            Slug = "foto-anic", Name = "Foto Anić", CategorySlug = "foto-i-video", RegionSlug = "dalmacija",
            IsPublished = true, ClaimStatus = "claimed", OwnerUserId = user.Id,
        };
        db.Vendors.Add(vendor);
        db.SaveChanges();

        var storage = new FakeStorage();
        return (db, user, vendor, storage, ControllerAs(db, users, storage, user.Id));
    }

    private static string? GetErrorCode(object? value) =>
        value?.GetType().GetProperty("error")?.GetValue(value) as string;

    [Fact]
    public async Task Upload_WithoutRightsConfirmation_IsBadRequest_AndStoresNothing()
    {
        var (db, _, _, storage, controller) = await OwnedSetup();

        var res = await controller.Upload("foto-anic", PngFile(), rightsConfirmed: false, ct: CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(res.Result);
        Assert.Equal("rights_not_confirmed", GetErrorCode(bad.Value));
        Assert.Empty(storage.Saved);          // provjera je PRIJE obrade i pohrane slike
        Assert.Empty(db.VendorPhotos);
    }

    [Fact]
    public async Task Upload_WithRightsConfirmation_RecordsEvidence_AndIsPublicImmediately()
    {
        var (db, user, vendor, storage, controller) = await OwnedSetup();

        var res = await controller.Upload("foto-anic", PngFile(), rightsConfirmed: true, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(res.Result);
        var dto = Assert.IsType<ProviderPhotoDto>(ok.Value);
        Assert.Equal("unreviewed", dto.ModerationStatus);
        Assert.Null(dto.ModerationNote);

        var photo = Assert.Single(db.VendorPhotos.ToList());
        Assert.Equal("unreviewed", photo.ModerationStatus);
        Assert.Equal("partner", photo.Source);
        Assert.Equal(user.Id, photo.UploadedByUserId);
        Assert.NotNull(photo.RightsConfirmedAt);
        Assert.NotNull(photo.CreatedAt);
        Assert.Equal(2, storage.Saved.Count); // glavna slika + thumbnail

        // post-moderacija: slika NE čeka admina — javni prikaz je sadrži odmah
        var loaded = db.Vendors.Include(v => v.Photos).Single(v => v.Id == vendor.Id);
        var publicDto = VendorMapper.ToDto(loaded);
        Assert.Equal(new[] { photo.StorageKey }, publicDto.Photos!);
    }

    [Fact]
    public async Task Upload_ByNonOwner_IsForbidden_EvenWithRightsConfirmation()
    {
        var (db, users) = BuildServices();
        var owner = new AppUser { UserName = "vlasnik@primjer.hr", Email = "vlasnik@primjer.hr", EmailConfirmed = true };
        var stranger = new AppUser { UserName = "netko@primjer.hr", Email = "netko@primjer.hr", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(owner)).Succeeded);
        Assert.True((await users.CreateAsync(stranger)).Succeeded);
        db.Vendors.Add(new Vendor
        {
            Slug = "foto-anic", Name = "Foto Anić", CategorySlug = "foto-i-video", RegionSlug = "dalmacija",
            ClaimStatus = "claimed", OwnerUserId = owner.Id,
        });
        db.SaveChanges();
        var storage = new FakeStorage();

        var res = await ControllerAs(db, users, storage, stranger.Id)
            .Upload("foto-anic", PngFile(), rightsConfirmed: true, ct: CancellationToken.None);

        Assert.IsType<ForbidResult>(res.Result);
        Assert.Empty(storage.Saved);
        Assert.Empty(db.VendorPhotos);
    }
}
