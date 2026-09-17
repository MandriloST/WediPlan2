using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Controllers;

/// <summary>
/// Google OAuth (§5, način 1). Aktivan SAMO ako su postavljeni Google:ClientId/ClientSecret
/// (v. Program.cs) — inače rute vraćaju 404 preko IsEnabled provjere u frontendu (GET /api/auth/providers).
///
/// Tok: /api/auth/google  → redirect na Google → /api/auth/google/callback → kreira/nađe korisnika,
/// postavi Identity cookie, redirect natrag na frontend.
/// </summary>
[ApiController]
[Route("api/auth/google")]
public class ExternalAuthController : ControllerBase
{
    private readonly SignInManager<AppUser> _signIn;
    private readonly UserManager<AppUser> _users;
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;

    public ExternalAuthController(SignInManager<AppUser> signIn, UserManager<AppUser> users,
        AppDbContext db, IConfiguration cfg)
    {
        _signIn = signIn; _users = users; _db = db; _cfg = cfg;
    }

    private bool Enabled => !string.IsNullOrWhiteSpace(_cfg["Google:ClientId"]);
    private string FrontendUrl => (_cfg["App:PublicUrl"] ?? "http://localhost:3000").TrimEnd('/');

    /// <summary>Start: preusmjeri na Google. `returnTo` = putanja na frontendu nakon prijave.</summary>
    [HttpGet]
    public IActionResult Start([FromQuery] string? returnTo)
    {
        if (!Enabled) return NotFound();
        var redirectUrl = Url.Action(nameof(Callback), "ExternalAuth", new { returnTo }) ?? "/api/auth/google/callback";
        var props = _signIn.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirectUrl);
        return Challenge(props, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? returnTo)
    {
        if (!Enabled) return NotFound();
        var safe = SafeReturn(returnTo);

        var info = await _signIn.GetExternalLoginInfoAsync();
        if (info == null) return Redirect($"{FrontendUrl}/prijava?error=google");

        // već povezan Google login?
        var signIn = await _signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey,
            isPersistent: true, bypassTwoFactor: true);
        if (signIn.Succeeded) return Redirect(FrontendUrl + safe);

        var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email)) return Redirect($"{FrontendUrl}/prijava?error=google_no_email");
        email = email.Trim().ToLowerInvariant();

        // poveži s postojećim računom istog emaila ili kreiraj novog
        var user = await _users.FindByEmailAsync(email);
        if (user == null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true, // Google je potvrdio email
                DisplayName = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value,
            };
            var created = await _users.CreateAsync(user);
            if (!created.Succeeded) return Redirect($"{FrontendUrl}/prijava?error=google");
            await _users.AddToRoleAsync(user, Roles.Couple);
        }

        await _users.AddLoginAsync(user, info); // veže Google identitet uz račun
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _signIn.SignInAsync(user, isPersistent: true);
        return Redirect(FrontendUrl + safe);
    }

    /// <summary>Dozvoli samo relativne putanje (spriječi open-redirect na tuđe domene).</summary>
    private static string SafeReturn(string? returnTo) =>
        !string.IsNullOrEmpty(returnTo) && returnTo.StartsWith('/') && !returnTo.StartsWith("//")
            ? returnTo : "/";
}
