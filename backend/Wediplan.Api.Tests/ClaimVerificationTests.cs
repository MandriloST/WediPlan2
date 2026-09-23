using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wediplan.Api.Auth;
using Wediplan.Api.Contracts;
using Wediplan.Api.Controllers;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;
using Wediplan.Api.Services;
using Xunit;
using DomainClaim = Wediplan.Api.Domain.Claim; // "Claim" je dvosmisleno sa System.Security.Claims.Claim

namespace Wediplan.Api.Tests;

/// <summary>
/// Testira e-mail verifikaciju claima (§Zadatak 5, PLAN-PRIORITETI-LANSIRANJE-2.md) nad EF
/// InMemory bazom — isti obrazac kao <see cref="ReviewsControllerTests"/>: pravi UserManager
/// preko AddIdentityCore, kontroler s ručno postavljenim HttpContext.User.
/// </summary>
public class ClaimVerificationTests
{
    /// <summary>Bilježi zadnji poslani mail bez stvarnog slanja — nema mocking biblioteke u projektu.</summary>
    private class FakeEmailSender : IEmailSender
    {
        public int SentCount;
        public Task SendAsync(string toEmail, string subject, string htmlBody, string textBody, CancellationToken ct = default)
        {
            SentCount++;
            return Task.CompletedTask;
        }
    }

    private static (AppDbContext Db, UserManager<AppUser> Users, FakeEmailSender Emails, ClaimsController Controller)
        Build(bool autoApprove = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<AppUser>(o => o.User.RequireUniqueEmail = true)
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
            {
                ["Claims:AutoApproveOnEmailVerify"] = autoApprove ? "true" : "false",
            })
            .Build();
        var emails = new FakeEmailSender();
        var authEmails = new AuthEmails(emails, cfg);
        var approval = new ClaimApprovalService(db);

        // Principal se postavlja tek preko SetUser() nakon što je pravi korisnik seedan.
        var controller = new ClaimsController(db, users, authEmails, approval, cfg);
        return (db, users, emails, controller);
    }

    private static Vendor SeedVendor(AppDbContext db, string? email = "vlasnik@primjer.hr", string slug = "test-dvorana")
    {
        var vendor = new Vendor
        {
            Slug = slug,
            Name = "Test Dvorana",
            CategorySlug = "restorani-i-sale",
            RegionSlug = "dalmacija",
            IsPublished = true,
            OptOut = false,
            Email = email,
        };
        db.Vendors.Add(vendor);
        db.SaveChanges();
        return vendor;
    }

    private async Task<AppUser> SeedUserAsync(UserManager<AppUser> users, string email = "par@example.com")
    {
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }

    private static DomainClaim SeedPendingClaim(AppDbContext db, Guid userId, Guid vendorId)
    {
        var claim = new DomainClaim { VendorId = vendorId, UserId = userId, Evidence = "", Status = "pending" };
        db.Claims.Add(claim);
        db.SaveChanges();
        return claim;
    }

    // ---------------------------------------------------------------- send-verification

    [Fact]
    public async Task SendVerification_ReturnsBadRequest_WhenVendorHasNoEmail()
    {
        var (db, users, _, controller) = Build();
        var user = await SeedUserAsync(users);
        var vendor = SeedVendor(db, email: null);
        var claim = SeedPendingClaim(db, user.Id, vendor.Id);
        SetUser(controller, user.Id);

        var result = await controller.SendVerification(claim.Id, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("no_email_on_file", GetErrorCode(bad.Value));
    }

    [Fact]
    public async Task SendVerification_CreatesToken_AndSendsEmail()
    {
        var (db, users, emails, controller) = Build();
        var user = await SeedUserAsync(users);
        var vendor = SeedVendor(db);
        var claim = SeedPendingClaim(db, user.Id, vendor.Id);
        SetUser(controller, user.Id);

        var result = await controller.SendVerification(claim.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, emails.SentCount);
        Assert.Equal(1, db.ClaimVerificationTokens.Count(t => t.ClaimId == claim.Id));
    }

    [Fact]
    public async Task SendVerification_RateLimits_SecondImmediateRequest()
    {
        var (db, users, _, controller) = Build();
        var user = await SeedUserAsync(users);
        var vendor = SeedVendor(db);
        var claim = SeedPendingClaim(db, user.Id, vendor.Id);
        SetUser(controller, user.Id);

        await controller.SendVerification(claim.Id, CancellationToken.None);
        var second = await controller.SendVerification(claim.Id, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(second);
        Assert.Equal(StatusCodes.Status429TooManyRequests, obj.StatusCode);
        Assert.Equal("too_many_requests", GetErrorCode(obj.Value));
    }

    // ---------------------------------------------------------------- verify

    [Fact]
    public async Task Verify_ApprovesClaim_WhenTokenValid_AndAutoApproveOn()
    {
        var (db, users, _, controller) = Build(autoApprove: true);
        var user = await SeedUserAsync(users);
        var vendor = SeedVendor(db);
        var claim = SeedPendingClaim(db, user.Id, vendor.Id);
        SetUser(controller, user.Id);
        var raw = Tokens.NewRaw();
        db.ClaimVerificationTokens.Add(new ClaimVerificationToken
        { ClaimId = claim.Id, TokenHash = Tokens.Hash(raw), ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await db.SaveChangesAsync();

        var result = await controller.Verify(new VerifyClaimRequest(raw), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("approved", GetStatus(ok.Value));
        var reloadedClaim = await db.Claims.FirstAsync(c => c.Id == claim.Id);
        Assert.Equal("approved", reloadedClaim.Status);
        Assert.Equal("email_verified", reloadedClaim.Evidence);
        var reloadedVendor = await db.Vendors.FirstAsync(v => v.Id == vendor.Id);
        Assert.Equal("claimed", reloadedVendor.ClaimStatus);
        Assert.Equal(user.Id, reloadedVendor.OwnerUserId);
    }

    [Fact]
    public async Task Verify_OnlyRecordsEvidence_WhenAutoApproveOff()
    {
        var (db, users, _, controller) = Build(autoApprove: false);
        var user = await SeedUserAsync(users);
        var vendor = SeedVendor(db);
        var claim = SeedPendingClaim(db, user.Id, vendor.Id);
        SetUser(controller, user.Id);
        var raw = Tokens.NewRaw();
        db.ClaimVerificationTokens.Add(new ClaimVerificationToken
        { ClaimId = claim.Id, TokenHash = Tokens.Hash(raw), ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await db.SaveChangesAsync();

        var result = await controller.Verify(new VerifyClaimRequest(raw), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("verified", GetStatus(ok.Value));
        var reloadedClaim = await db.Claims.FirstAsync(c => c.Id == claim.Id);
        Assert.Equal("pending", reloadedClaim.Status); // NIJE auto-odobreno
        Assert.Equal("email_verified", reloadedClaim.Evidence); // ali dokaz je zabilježen
    }

    [Fact]
    public async Task Verify_ReturnsBadRequest_WhenTokenExpired()
    {
        var (db, users, _, controller) = Build();
        var user = await SeedUserAsync(users);
        var vendor = SeedVendor(db);
        var claim = SeedPendingClaim(db, user.Id, vendor.Id);
        SetUser(controller, user.Id);
        var raw = Tokens.NewRaw();
        db.ClaimVerificationTokens.Add(new ClaimVerificationToken
        { ClaimId = claim.Id, TokenHash = Tokens.Hash(raw), ExpiresAt = DateTime.UtcNow.AddHours(-1) }); // već istekao
        await db.SaveChangesAsync();

        var result = await controller.Verify(new VerifyClaimRequest(raw), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("invalid_token", GetErrorCode(bad.Value));
    }

    [Fact]
    public async Task Verify_ReturnsBadRequest_WhenTokenAlreadyConsumed()
    {
        var (db, users, _, controller) = Build();
        var user = await SeedUserAsync(users);
        var vendor = SeedVendor(db);
        var claim = SeedPendingClaim(db, user.Id, vendor.Id);
        SetUser(controller, user.Id);
        var raw = Tokens.NewRaw();
        db.ClaimVerificationTokens.Add(new ClaimVerificationToken
        {
            ClaimId = claim.Id, TokenHash = Tokens.Hash(raw),
            ExpiresAt = DateTime.UtcNow.AddHours(24), ConsumedAt = DateTime.UtcNow.AddMinutes(-5), // već potrošen
        });
        await db.SaveChangesAsync();

        var result = await controller.Verify(new VerifyClaimRequest(raw), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("invalid_token", GetErrorCode(bad.Value));
    }

    [Fact]
    public async Task Verify_ReturnsBadRequest_WhenTokenUnknown()
    {
        var (_, users, _, controller) = Build();
        var user = await SeedUserAsync(users);
        SetUser(controller, user.Id);

        var result = await controller.Verify(new VerifyClaimRequest("nepostojeci-token"), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("invalid_token", GetErrorCode(bad.Value));
    }

    [Fact]
    public async Task Verify_ReturnsForbidden_WhenTokenBelongsToAnotherUsersClaim()
    {
        var (db, users, _, controller) = Build();
        var owner = await SeedUserAsync(users, "vlasnik-claima@example.com");
        var stranger = await SeedUserAsync(users, "netko-drugi@example.com");
        var vendor = SeedVendor(db);
        var claim = SeedPendingClaim(db, owner.Id, vendor.Id);
        var raw = Tokens.NewRaw();
        db.ClaimVerificationTokens.Add(new ClaimVerificationToken
        { ClaimId = claim.Id, TokenHash = Tokens.Hash(raw), ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await db.SaveChangesAsync();
        SetUser(controller, stranger.Id); // prijavljen je NETKO DRUGI, ne vlasnik claima

        var result = await controller.Verify(new VerifyClaimRequest(raw), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        Assert.Equal("not_your_claim", GetErrorCode(obj.Value));
    }

    /// <summary>Postavi "prijavljenog" korisnika na kontroleru (isti obrazac kao ReviewsControllerTests).</summary>
    private static void SetUser(ClaimsController controller, Guid userId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "TestAuth"));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
    }

    private static string? GetErrorCode(object? value) =>
        value?.GetType().GetProperty("error")?.GetValue(value) as string;

    private static string? GetStatus(object? value) =>
        value?.GetType().GetProperty("status")?.GetValue(value) as string;
}
