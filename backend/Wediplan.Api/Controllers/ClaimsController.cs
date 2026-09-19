using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Contracts;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Controllers;

/// <summary>
/// Preuzimanje profila (§6). Prijavljeni korisnik traži claim nad pružateljem; claim ide u
/// <c>pending</c>, korisnik dobiva rolu <c>provider</c> i pristup uređivanju DRAFTA (javni profil
/// se ne mijenja do odobrenja admina). Evidence <c>domain_match</c> izvodi se iz e-mail domene.
/// </summary>
[ApiController]
[Route("api/claims")]
[Authorize]
public class ClaimsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    public ClaimsController(AppDbContext db, UserManager<AppUser> users) { _db = db; _users = users; }

    private Guid Uid() => Guid.Parse(_users.GetUserId(User)!);

    /// <summary>POST /api/claims — zatraži preuzimanje profila.</summary>
    [HttpPost]
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

    private async Task<ClaimDto> ToDto(Claim c, Vendor v) =>
        await Task.FromResult(new ClaimDto(c.Id.ToString(), v.Slug, v.Name, c.Status, c.Evidence, c.CreatedAt));
}
