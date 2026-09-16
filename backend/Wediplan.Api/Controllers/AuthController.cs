using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Auth;
using Wediplan.Api.Contracts;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Controllers;

/// <summary>
/// Auth (§5): email+lozinka, passwordless magic-link, potvrda emaila, reset lozinke.
/// Google OAuth je u zasebnim rutama (ExternalAuthController). Sesija = Identity aplikacijski
/// httpOnly cookie (postavlja SignInManager); klijent nikad ne vidi token.
///
/// Sigurnosna načela:
///  - odgovori ne otkrivaju postoji li email (enumeracija) — "ako račun postoji, poslali smo…";
///  - tokeni se spremaju kao hash (v. Tokens), jednokratni su i istječu;
///  - rate-limit po emailu/IP-u na slanje linkova (MagicLink/verify) da se izbjegne spam.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly SignInManager<AppUser> _signIn;
    private readonly AppDbContext _db;
    private readonly AuthEmails _emails;
    private readonly ILogger<AuthController> _log;

    public AuthController(UserManager<AppUser> users, SignInManager<AppUser> signIn,
        AppDbContext db, AuthEmails emails, ILogger<AuthController> log)
    {
        _users = users; _signIn = signIn; _db = db; _emails = emails; _log = log;
    }

    private static string Norm(string email) => email.Trim().ToLowerInvariant();
    private string Ip() => Infrastructure.ClientIp.Get(HttpContext);

    // ---------------------------------------------------------------- register (email+lozinka)
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var email = Norm(req.Email);
        var existing = await _users.FindByEmailAsync(email);
        if (existing != null)
        {
            // Ne otkrivaj postojanje računa. Ako postoji bez lozinke (magic/google), tiho ne diramo.
            return Ok(new { message = "Ako račun ne postoji, poslali smo poveznicu za potvrdu." });
        }

        var user = new AppUser { UserName = email, Email = email, DisplayName = req.DisplayName?.Trim() };
        var result = await _users.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { error = "weak_password", details = result.Errors.Select(e => e.Description) });

        await _users.AddToRoleAsync(user, Roles.Couple);
        await SendVerificationEmail(user, ct);
        return Ok(new { message = "Ako račun ne postoji, poslali smo poveznicu za potvrdu." });
    }

    // ---------------------------------------------------------------- login (email+lozinka)
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var email = Norm(req.Email);
        var user = await _users.FindByEmailAsync(email);
        if (user == null || user.PasswordHash == null)
            return Unauthorized(new { error = "invalid_credentials" });

        if (!await _users.IsEmailConfirmedAsync(user))
            return Unauthorized(new { error = "email_not_confirmed" });

        var result = await _signIn.PasswordSignInAsync(user, req.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut) return StatusCode(429, new { error = "locked_out" });
        if (!result.Succeeded) return Unauthorized(new { error = "invalid_credentials" });

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(await MeFor(user));
    }

    // ---------------------------------------------------------------- magic link: zatraži
    [HttpPost("magic/request")]
    public async Task<IActionResult> RequestMagic([FromBody] EmailOnlyRequest req, CancellationToken ct)
    {
        var email = Norm(req.Email);
        var ip = Ip();

        // rate-limit: max 3 aktivna linka po emailu u zadnjih 15 min
        var since = DateTime.UtcNow.AddMinutes(-15);
        var recent = await _db.MagicLinks.CountAsync(m => m.Email == email && m.CreatedAt > since, ct);
        if (recent >= 3)
            return Ok(new { message = "Ako račun postoji, poslali smo poveznicu za prijavu." });

        // korisnik se kreira tek pri PRVOM uspješnom consume-u (ne ovdje) da se izbjegne
        // stvaranje praznih računa spamom. Link vrijedi i za postojeće i za nove.
        var raw = Tokens.NewRaw();
        _db.MagicLinks.Add(new MagicLink
        {
            Email = email,
            TokenHash = Tokens.Hash(raw),
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            RequestIp = ip,
        });
        await _db.SaveChangesAsync(ct);

        try { await _emails.SendMagicLink(email, raw, ct); }
        catch (Exception e) { _log.LogError(e, "magic link email"); }

        return Ok(new { message = "Ako račun postoji, poslali smo poveznicu za prijavu." });
    }

    // ---------------------------------------------------------------- magic link: iskoristi
    [HttpPost("magic/consume")]
    public async Task<IActionResult> ConsumeMagic([FromBody] MagicConsumeRequest req, CancellationToken ct)
    {
        var hash = Tokens.Hash(req.Token);
        var link = await _db.MagicLinks.FirstOrDefaultAsync(m => m.TokenHash == hash, ct);
        if (link == null || link.ConsumedAt != null || link.ExpiresAt < DateTime.UtcNow)
            return Unauthorized(new { error = "invalid_or_expired" });

        link.ConsumedAt = DateTime.UtcNow;

        var user = await _users.FindByEmailAsync(link.Email);
        if (user == null)
        {
            // prvi put: kreiraj korisnika (bez lozinke), email je time potvrđen
            user = new AppUser { UserName = link.Email, Email = link.Email, EmailConfirmed = true };
            var created = await _users.CreateAsync(user);
            if (!created.Succeeded) return StatusCode(500, new { error = "user_create_failed" });
            await _users.AddToRoleAsync(user, Roles.Couple);
        }
        else if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true; // prijava linkom dokazuje vlasništvo emaila
            await _users.UpdateAsync(user);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _signIn.SignInAsync(user, isPersistent: true);
        return Ok(await MeFor(user));
    }

    // ---------------------------------------------------------------- potvrda emaila
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest req, CancellationToken ct)
    {
        var hash = Tokens.Hash(req.Token);
        var tok = await _db.EmailVerificationTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (tok == null || tok.ConsumedAt != null || tok.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { error = "invalid_or_expired" });

        var user = await _users.FindByIdAsync(tok.UserId.ToString());
        if (user == null) return BadRequest(new { error = "invalid_or_expired" });

        tok.ConsumedAt = DateTime.UtcNow;
        user.EmailConfirmed = true;
        await _users.UpdateAsync(user);
        await _db.SaveChangesAsync(ct);

        // po potvrdi automatski prijavi (jedan korak manje)
        await _signIn.SignInAsync(user, isPersistent: true);
        return Ok(await MeFor(user));
    }

    // ---------------------------------------------------------------- reset lozinke: zatraži
    [HttpPost("password/forgot")]
    public async Task<IActionResult> Forgot([FromBody] EmailOnlyRequest req, CancellationToken ct)
    {
        var email = Norm(req.Email);
        var user = await _users.FindByEmailAsync(email);
        if (user != null && user.PasswordHash != null)
        {
            var raw = await _users.GeneratePasswordResetTokenAsync(user); // Identity token (ne naša tablica)
            try { await _emails.SendPasswordReset(email, raw, ct); }
            catch (Exception e) { _log.LogError(e, "reset email"); }
        }
        return Ok(new { message = "Ako račun postoji, poslali smo upute za novu lozinku." });
    }

    // ---------------------------------------------------------------- reset lozinke: postavi
    [HttpPost("password/reset")]
    public async Task<IActionResult> Reset([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(Norm(req.Email));
        if (user == null) return BadRequest(new { error = "invalid_or_expired" });

        var result = await _users.ResetPasswordAsync(user, req.Token, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { error = "invalid_or_expired", details = result.Errors.Select(e => e.Description) });

        if (!user.EmailConfirmed) { user.EmailConfirmed = true; await _users.UpdateAsync(user); }
        return Ok(new { message = "Lozinka je promijenjena. Možete se prijaviti." });
    }

    // ---------------------------------------------------------------- odjava
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return Ok(new { message = "Odjavljeni ste." });
    }

    // ---------------------------------------------------------------- helpers
    private async Task SendVerificationEmail(AppUser user, CancellationToken ct)
    {
        var raw = Tokens.NewRaw();
        _db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            UserId = user.Id,
            TokenHash = Tokens.Hash(raw),
            ExpiresAt = DateTime.UtcNow.AddHours(24),
        });
        await _db.SaveChangesAsync(ct);
        try { await _emails.SendVerification(user.Email!, raw, ct); }
        catch (Exception e) { _log.LogError(e, "verify email"); }
    }

    private async Task<MeDto> MeFor(AppUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        return new MeDto(user.Id.ToString(), user.Email!, user.DisplayName, user.EmailConfirmed, roles.ToList());
    }
}
