using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Auth;
using Wediplan.Api.Contracts;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;
using Wediplan.Api.Services;

namespace Wediplan.Api.Controllers;

/// <summary>
/// Preuzimanje profila (§6). Prijavljeni korisnik traži claim nad pružateljem; claim ide u
/// <c>pending</c>, korisnik dobiva rolu <c>provider</c> i pristup uređivanju DRAFTA (javni profil
/// se ne mijenja do odobrenja admina). Evidence <c>domain_match</c> izvodi se iz e-mail domene;
/// <c>email_verified</c> (§Zadatak 5) potvrdom poveznice poslane na Vendor.Email.
/// </summary>
[ApiController]
[Route("api/claims")]
[Authorize]
public class ClaimsController : ControllerBase
{
    // Anti-zloupotreba slanja verifikacijskog maila (§Zadatak 5): max ovoliko nepotrošenih
    // tokena po claimu unutar 24h, i minimalni razmak između dva slanja.
    private const int MaxTokensPer24h = 3;
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(2);

    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly AuthEmails _emails;
    private readonly ClaimApprovalService _approval;
    private readonly IConfiguration _cfg;

    public ClaimsController(AppDbContext db, UserManager<AppUser> users, AuthEmails emails,
        ClaimApprovalService approval, IConfiguration cfg)
    {
        _db = db; _users = users; _emails = emails; _approval = approval; _cfg = cfg;
    }

    private Guid Uid() => Guid.Parse(_users.GetUserId(User)!);

    /// <summary>POST /api/claims — zatraži preuzimanje profila.</summary>
    [HttpPost]
    [EnableRateLimiting("writes")] // §Zadatak 9
    public async Task<ActionResult<ClaimDto>> Create([FromBody] ClaimRequest req, CancellationToken ct)
    {
        var uid = Uid();
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Slug == req.VendorSlug, ct);
        if (vendor == null || !vendor.IsPublished || vendor.OptOut) return NotFound(new { error = "vendor_not_found" });
        if (vendor.ClaimStatus == "claimed") return Conflict(new { error = "already_claimed" });

        // Postojeći zahtjev istog korisnika za istog pružatelja → vrati ga (idempotentno).
        var existing = await _db.Claims.FirstOrDefaultAsync(c => c.UserId == uid && c.VendorId == vendor.Id, ct);
        if (existing != null && existing.Status != "rejected")
            return Ok(await ToDto(existing, vendor));

        var user = await _users.FindByIdAsync(uid.ToString());
        var evidence =
            ProviderMapper.EmailDomain(user?.Email) is { } ed &&
            ProviderMapper.WebsiteDomain(vendor.Website) is { } wd &&
            ed == wd
                ? "domain_match" : "";

        if (existing is { Status: "rejected" })
        {
            // Dopusti ponovni pokušaj nakon odbijanja: resetiraj isti redak.
            existing.Message = req.Message?.Trim() ?? "";
            existing.Evidence = evidence;
            existing.Status = "pending";
            existing.DecidedBy = null; existing.DecidedAt = null;
            existing.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Claims.Add(new Claim
            {
                VendorId = vendor.Id, UserId = uid,
                Message = req.Message?.Trim() ?? "", Evidence = evidence,
            });
        }

        // Draft se popuni iz žive verzije da pružatelj odmah ima što uređivati (§6.3).
        if (!await _db.VendorDrafts.AnyAsync(d => d.VendorId == vendor.Id, ct))
            _db.VendorDrafts.Add(ProviderMapper.SeedFromVendor(vendor));

        await _db.SaveChangesAsync(ct);

        // Rola provider (idempotentno) — pristup nadzornoj ploči partnera.
        if (user != null && !await _users.IsInRoleAsync(user, Roles.Provider))
            await _users.AddToRoleAsync(user, Roles.Provider);

        var claim = existing ?? await _db.Claims.FirstAsync(c => c.UserId == uid && c.VendorId == vendor.Id, ct);
        return Ok(await ToDto(claim, vendor));
    }

    /// <summary>GET /api/claims/mine — svi zahtjevi prijavljenog korisnika.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<ClaimDto>>> Mine(CancellationToken ct)
    {
        var uid = Uid();
        var rows = await (
            from c in _db.Claims.AsNoTracking().Where(c => c.UserId == uid)
            join v in _db.Vendors.AsNoTracking() on c.VendorId equals v.Id
            orderby c.CreatedAt descending
            select new ClaimDto(c.Id.ToString(), v.Slug, v.Name, c.Status, c.Evidence, c.CreatedAt)
        ).ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>
    /// POST /api/claims/{id}/send-verification — pošalji jednokratni token na Vendor.Email (§Zadatak 5).
    /// Vraća maskiranu adresu (npr. "t***@domena.hr") — puna interna adresa se nikad ne izlaže korisniku.
    /// </summary>
    [HttpPost("{id:guid}/send-verification")]
    [EnableRateLimiting("writes")] // §Zadatak 9 — uz vlastiti (finiji) anti-abuse iznad
    public async Task<IActionResult> SendVerification(Guid id, CancellationToken ct)
    {
        var uid = Uid();
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == id && c.UserId == uid, ct);
        if (claim == null) return NotFound();
        if (claim.Status != "pending") return Conflict(new { error = "already_decided" });

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == claim.VendorId, ct);
        if (vendor == null) return NotFound(new { error = "vendor_not_found" });
        if (string.IsNullOrWhiteSpace(vendor.Email)) return BadRequest(new { error = "no_email_on_file" });

        var recent = await _db.ClaimVerificationTokens
            .Where(t => t.ClaimId == claim.Id && t.CreatedAt > DateTime.UtcNow.AddHours(-24))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        if (recent.Count >= MaxTokensPer24h || (recent.Count > 0 && recent[0].CreatedAt > DateTime.UtcNow - ResendCooldown))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "too_many_requests" });

        var raw = Tokens.NewRaw();
        _db.ClaimVerificationTokens.Add(new ClaimVerificationToken
        {
            ClaimId = claim.Id,
            TokenHash = Tokens.Hash(raw),
            ExpiresAt = DateTime.UtcNow.AddHours(24),
        });
        await _db.SaveChangesAsync(ct);

        await _emails.SendClaimVerification(vendor.Email!, vendor.Name, raw, ct);
        return Ok(new { sentTo = ProviderMapper.MaskEmail(vendor.Email!) });
    }

    /// <summary>
    /// POST /api/claims/verify — potvrdi token iz maila. Auto-odobri kad je claim pending i
    /// <c>Claims:AutoApproveOnEmailVerify</c> nije eksplicitno isključen (default true) — jednom
    /// izmjenom te postavke se ponašanje vraća na "jak dokaz + admin klik".
    /// </summary>
    [HttpPost("verify")]
    [EnableRateLimiting("writes")] // §Zadatak 9
    public async Task<IActionResult> Verify([FromBody] VerifyClaimRequest req, CancellationToken ct)
    {
        var uid = Uid();
        var hash = Tokens.Hash(req.Token);
        var token = await _db.ClaimVerificationTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token == null || token.ConsumedAt != null || token.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { error = "invalid_token" });

        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == token.ClaimId, ct);
        if (claim == null) return BadRequest(new { error = "invalid_token" });
        if (claim.UserId != uid) return StatusCode(StatusCodes.Status403Forbidden, new { error = "not_your_claim" });

        token.ConsumedAt = DateTime.UtcNow;
        claim.Evidence = "email_verified";

        if (claim.Status == "pending" && _cfg.GetValue("Claims:AutoApproveOnEmailVerify", true))
        {
            var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == claim.VendorId, ct);
            if (vendor != null)
            {
                await _approval.ApproveAsync(claim, vendor, decidedBy: null, ct);
                return Ok(new { status = "approved" });
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { status = "verified" });
    }

    private async Task<ClaimDto> ToDto(Claim c, Vendor v) =>
        await Task.FromResult(new ClaimDto(c.Id.ToString(), v.Slug, v.Name, c.Status, c.Evidence, c.CreatedAt));
}
