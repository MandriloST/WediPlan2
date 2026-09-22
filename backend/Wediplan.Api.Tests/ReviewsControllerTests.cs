using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wediplan.Api.Contracts;
using Wediplan.Api.Controllers;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;
using Xunit;

namespace Wediplan.Api.Tests;

/// <summary>
/// Testira <see cref="ReviewsController.Create"/> nad EF InMemory bazom (bez Postgresa — § Plan
/// prioriteti #1b). <c>UserManager&lt;AppUser&gt;</c> se gradi preko <c>AddIdentityCore</c> (isti
/// obrazac kao Program.cs), NE ručnom konstrukcijom — to je ono što Identity službeno preporučuje za
/// testove, jer ručno slaganje validatora/hashera je krhko.
/// </summary>
public class ReviewsControllerTests
{
    /// <summary>Svaki test dobiva vlastitu, praznu InMemory bazu (nasumično ime) + pravi UserManager.</summary>
    private static (AppDbContext Db, UserManager<AppUser> Users) BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<AppUser>(o => o.User.RequireUniqueEmail = true)
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return (scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>());
    }

    /// <summary>Kreira ReviewsController s HttpContext.User postavljenim na zadanog korisnika (kao da je prijavljen).</summary>
    private static ReviewsController ControllerAs(AppDbContext db, UserManager<AppUser> users, Guid userId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "TestAuth"));
        return new ReviewsController(db, users)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } },
        };
    }

    private static Vendor SeedVendor(AppDbContext db, string slug = "test-dvorana")
    {
        var vendor = new Vendor
        {
            Slug = slug,
            Name = "Test Dvorana",
            CategorySlug = "restorani-i-sale",
            RegionSlug = "dalmacija",
            IsPublished = true,
            OptOut = false,
        };
        db.Vendors.Add(vendor);
        db.SaveChanges();
        return vendor;
    }

    private async Task<AppUser> SeedUserAsync(UserManager<AppUser> users, bool emailConfirmed)
    {
        var user = new AppUser { UserName = "par@example.com", Email = "par@example.com", EmailConfirmed = emailConfirmed };
        var result = await users.CreateAsync(user);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }

    [Fact]
    public async Task Create_ReturnsForbidden_WhenEmailNotConfirmed()
    {
        var (db, users) = BuildServices();
        var user = await SeedUserAsync(users, emailConfirmed: false);
        var controller = ControllerAs(db, users, user.Id);
        var req = new ReviewRequest("bilo-koji-slug", 5, "Sve je bilo savršeno organizirano i profesionalno.");

        var result = await controller.Create(req, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, obj.StatusCode);
        Assert.Equal("email_not_confirmed", GetErrorCode(obj.Value));
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenAlreadyReviewed()
    {
        var (db, users) = BuildServices();
        var user = await SeedUserAsync(users, emailConfirmed: true);
        var vendor = SeedVendor(db);
        db.UserReviews.Add(new UserReview { VendorId = vendor.Id, UserId = user.Id, Rating = 4, Text = "Prethodna recenzija istog korisnika.", Status = "pending" });
        await db.SaveChangesAsync();

        var controller = ControllerAs(db, users, user.Id);
        var req = new ReviewRequest(vendor.Slug, 5, "Pokušaj druge recenzije istog pružatelja od istog korisnika.");

        var result = await controller.Create(req, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("already_reviewed", GetErrorCode(conflict.Value));
    }

    [Fact]
    public async Task Create_Succeeds_WhenConfirmedAndFirstReview()
    {
        var (db, users) = BuildServices();
        var user = await SeedUserAsync(users, emailConfirmed: true);
        var vendor = SeedVendor(db);
        var controller = ControllerAs(db, users, user.Id);
        var req = new ReviewRequest(vendor.Slug, 5, "Odlična usluga, sve preporuke za buduće mladence.");

        var result = await controller.Create(req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, db.UserReviews.Count(r => r.UserId == user.Id && r.VendorId == vendor.Id));
    }

    /// <summary>Čita `error` s anonimnog objekta koji kontroleri vraćaju (`new { error = "..." }`).</summary>
    private static string? GetErrorCode(object? value) =>
        value?.GetType().GetProperty("error")?.GetValue(value) as string;
}
