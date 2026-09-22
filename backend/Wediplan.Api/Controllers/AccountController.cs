using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Contracts;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Controllers;

/// <summary>
/// Račun korisnika — brisanje ("pravo na zaborav", §9, Plan prioriteti #2). Pravila privatnosti
/// obećavaju brisanje računa; ovo je taj endpoint.
///
/// Što se briše (osobni podaci): AppUser (+ Identity: role, external login, tokeni — briše ih
/// ASP.NET Identity kaskadno na razini baze, isto kao <c>Favorite</c>/<c>BudgetPlan</c>/<c>Claim</c>/
/// <c>UserReview</c> — sve četiri već imaju pravi FK s ON DELETE CASCADE prema korisniku, v.
/// AppDbContext.OnModelCreating, pa ih NE brišemo ručno; <c>UserManager.DeleteAsync</c> je dovoljan).
/// Ručno ovdje brišemo samo ono što NEMA takav FK: <c>EmailVerificationToken</c> (ima samo indeks,
/// ne i cascade FK) i <c>MagicLink</c> (vezan uz email, ne uz UserId — nema kolonu za FK).
///
/// Što OSTAJE (poslovni, ne osobni podatak): profil pružatelja (<c>Vendor</c>) ostaje javan, samo mu
/// se <c>OwnerUserId</c> postavlja na null — brisanje korisničkog računa ne smije ukloniti tuđe
/// (buduće) rezervacije ili nečiji poslovni unos iz kataloga.
/// </summary>
[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly SignInManager<AppUser> _signIn;
    private readonly ILogger<AccountController> _log;

    public AccountController(AppDbContext db, UserManager<AppUser> users, SignInManager<AppUser> signIn,
        ILogger<AccountController> log)
    {
        _db = db; _users = users; _signIn = signIn; _log = log;
    }

    private Guid Uid() => Guid.Parse(_users.GetUserId(User)!);

    /// <summary>DELETE /api/account — trajno briše račun. Traži {"confirm":"OBRISI"} u tijelu.</summary>
    [HttpDelete]
    public async Task<IActionResult> Delete([FromBody] DeleteAccountRequest req, CancellationToken ct)
    {
        if (!string.Equals(req.Confirm?.Trim(), "OBRISI", StringComparison.Ordinal))
            return BadRequest(new { error = "confirmation_required" });

        var uid = Uid();
        var user = await _users.FindByIdAsync(uid.ToString());
        if (user == null) return Unauthorized();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Vendor ostaje (poslovni podatak) — samo se odvezuje vlasništvo. Korisnik može biti
        // vlasnik i više profila (npr. lanac dvorana), pa ExecuteUpdate pokriva sve odjednom.
        await _db.Vendors.Where(v => v.OwnerUserId == uid)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.OwnerUserId, (Guid?)null), ct);

        // Ne prate se FK-om (v. komentar klase gore) — brišu se eksplicitno.
        await _db.EmailVerificationTokens.Where(t => t.UserId == uid).ExecuteDeleteAsync(ct);
        await _db.MagicLinks.Where(m => m.Email == user.Email!).ExecuteDeleteAsync(ct);

        // Favorite, BudgetPlan, Claim, UserReview: brišu se automatski (DB ON DELETE CASCADE)
        // kad UserManager obriše red u users. Identity role/login/token/claim isto tako.
        var result = await _users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return StatusCode(500, new { error = "delete_failed", details = result.Errors.Select(e => e.Description) });
        }

        await tx.CommitAsync(ct);

        // Audit bez emaila/PII (minimizacija) — samo da je do brisanja došlo.
        _log.LogWarning("account deleted: user={UserId}", uid);

        await _signIn.SignOutAsync();
        return Ok(new { ok = true });
    }
}
