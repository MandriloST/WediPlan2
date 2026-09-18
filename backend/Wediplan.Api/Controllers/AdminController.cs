using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Contracts;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Controllers;

/// <summary>
/// Minimalno admin sučelje (§6, faza 4): lista pending claimova (odobri/odbij), lista pending
/// korisničkih recenzija (moderacija), publish/unpublish pružatelja. Sve rute traže rolu admin.
/// Admin se dodjeljuje jednokratno CLI-jem: `dotnet run -- --make-admin email@primjer.hr`.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    public AdminController(AppDbContext db, UserManager<AppUser> users) { _db = db; _users = users; }

    private Guid Uid() => Guid.Parse(_users.GetUserId(User)!);

    // ---------------------------------------------------------------- claimovi
    /// <summary>GET /api/admin/claims?status=pending — zahtjevi za moderaciju.</summary>
    [HttpGet("claims")]
    public async Task<ActionResult<IEnumerable<AdminClaimDto>>> Claims([FromQuery] string status = "pending", CancellationToken ct = default)
    {
        var rows = await (
            from c in _db.Claims.AsNoTracking().Where(c => c.Status == status)
            join v in _db.Vendors.AsNoTracking() on c.VendorId equals v.Id
            join u in _db.Users.AsNoTracking() on c.UserId equals u.Id
            orderby c.CreatedAt
            select new AdminClaimDto(
                c.Id.ToString(), v.Slug, v.Name, u.Email!, u.DisplayName,
                c.Message, c.Evidence, c.Status, c.CreatedAt)
        ).ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>
    /// POST /api/admin/claims/{id}/approve — odobri: objavi draft, postavi claimed + owner,
    /// i odbij ostale pending zahtjeve za istog pružatelja (jedan vlasnik).
    /// </summary>
    [HttpPost("claims/{id:guid}/approve")]
    public async Task<IActionResult> ApproveClaim(Guid id, CancellationToken ct)
    {
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (claim == null) return NotFound();
        if (claim.Status != "pending") return Conflict(new { error = "already_decided" });

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == claim.VendorId, ct);
        if (vendor == null) return NotFound(new { error = "vendor_not_found" });

        // Objavi draft (ako postoji) u živu verziju.
        var draft = await _db.VendorDrafts.FirstOrDefaultAsync(d => d.VendorId == vendor.Id, ct);
        if (draft != null) ProviderMapper.ApplyToVendor(draft, vendor);

        vendor.ClaimStatus = "claimed";
        vendor.OwnerUserId = claim.UserId;
        vendor.UpdatedAt = DateTime.UtcNow;

        claim.Status = "approved"; claim.DecidedBy = Uid(); claim.DecidedAt = DateTime.UtcNow;

        // Ostali pending zahtjevi za istog pružatelja → odbijeni.
        var others = await _db.Claims
            .Where(c => c.VendorId == vendor.Id && c.Id != claim.Id && c.Status == "pending")
            .ToListAsync(ct);
        foreach (var o in others) { o.Status = "rejected"; o.DecidedBy = Uid(); o.DecidedAt = DateTime.UtcNow; }

        await _db.SaveChangesAsync(ct);
        return Ok(new { status = "approved" });
    }

    /// <summary>POST /api/admin/claims/{id}/reject.</summary>
    [HttpPost("claims/{id:guid}/reject")]
    public async Task<IActionResult> RejectClaim(Guid id, CancellationToken ct)
    {
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (claim == null) return NotFound();
        if (claim.Status != "pending") return Conflict(new { error = "already_decided" });
        claim.Status = "rejected"; claim.DecidedBy = Uid(); claim.DecidedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { status = "rejected" });
    }

    // ---------------------------------------------------------------- recenzije
    /// <summary>GET /api/admin/reviews?status=pending — recenzije za moderaciju.</summary>
    [HttpGet("reviews")]
    public async Task<ActionResult<IEnumerable<AdminReviewDto>>> Reviews([FromQuery] string status = "pending", CancellationToken ct = default)
    {
        var rows = await (
            from r in _db.UserReviews.AsNoTracking().Where(r => r.Status == status)
            join v in _db.Vendors.AsNoTracking() on r.VendorId equals v.Id
            join u in _db.Users.AsNoTracking() on r.UserId equals u.Id
            orderby r.CreatedAt
            select new AdminReviewDto(
                r.Id.ToString(), v.Slug, v.Name, u.Email!, r.Rating, r.Text, r.Status, r.CreatedAt)
        ).ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>POST /api/admin/reviews/{id}/approve — objavi recenziju.</summary>
    [HttpPost("reviews/{id:guid}/approve")]
    public async Task<IActionResult> ApproveReview(Guid id, CancellationToken ct)
    {
        var r = await _db.UserReviews.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r == null) return NotFound();
        if (r.Status != "pending") return Conflict(new { error = "already_decided" });
        r.Status = "published"; r.DecidedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { status = "published" });
    }

    /// <summary>POST /api/admin/reviews/{id}/reject.</summary>
    [HttpPost("reviews/{id:guid}/reject")]
    public async Task<IActionResult> RejectReview(Guid id, CancellationToken ct)
    {
        var r = await _db.UserReviews.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r == null) return NotFound();
        if (r.Status != "pending") return Conflict(new { error = "already_decided" });
        r.Status = "rejected"; r.DecidedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { status = "rejected" });
    }

    // ---------------------------------------------------------------- GDPR opt-out (§9, faza 6)
    /// <summary>GET /api/admin/optouts — skriveni profili (opt-out), za pregled i eventualno vraćanje.</summary>
    [HttpGet("optouts")]
    public async Task<ActionResult<IEnumerable<AdminOptOutDto>>> OptOuts(CancellationToken ct)
    {
        var rows = await _db.Vendors.AsNoTracking().Where(v => v.OptOut)
            .OrderBy(v => v.Name)
            .Select(v => new AdminOptOutDto(v.Slug, v.Name, v.CategorySlug, v.IsPublished))
            .ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>POST /api/admin/vendors/{slug}/restore-optout — poništi opt-out (npr. zloupotreba).</summary>
    [HttpPost("vendors/{slug}/restore-optout")]
    public async Task<IActionResult> RestoreOptOut(string slug, CancellationToken ct)
    {
        var v = await _db.Vendors.FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (v == null) return NotFound();
        v.OptOut = false;
        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    // ---------------------------------------------------------------- publish/unpublish (§6, §9)
    /// <summary>POST /api/admin/vendors/{slug}/unpublish — skini profil iz javnog prikaza.</summary>
    [HttpPost("vendors/{slug}/unpublish")]
    public async Task<IActionResult> Unpublish(string slug, CancellationToken ct) => await SetPublished(slug, false, ct);

    /// <summary>POST /api/admin/vendors/{slug}/publish — vrati profil u javni prikaz.</summary>
    [HttpPost("vendors/{slug}/publish")]
    public async Task<IActionResult> Republish(string slug, CancellationToken ct) => await SetPublished(slug, true, ct);

    private async Task<IActionResult> SetPublished(string slug, bool published, CancellationToken ct)
    {
        var v = await _db.Vendors.FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (v == null) return NotFound();
        v.IsPublished = published; v.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { slug, isPublished = published });
    }
}
