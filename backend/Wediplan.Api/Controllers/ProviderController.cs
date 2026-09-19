using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Contracts;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Controllers;

/// <summary>
/// Nadzorna ploča partnera (§6.5). Pružatelj vidi pružatelje koje je preuzeo ili čeka odobrenje,
/// uređuje DRAFT (about/usluge/cijena/stil), vidi osnovnu statistiku (pregledi/usporedbe/favoriti,
/// 30 dana — iz daily_stats), i — samo ako je odobreni vlasnik — objavljuje svoj draft.
/// Pending pružatelji uređuju, ali objavu radi admin pri odobrenju claima.
/// </summary>
[ApiController]
[Route("api/provider")]
[Authorize]
public class ProviderController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    public ProviderController(AppDbContext db, UserManager<AppUser> users) { _db = db; _users = users; }

    private Guid Uid() => Guid.Parse(_users.GetUserId(User)!);

    /// <summary>GET /api/provider/vendors — svi pružatelji vezani uz ovog korisnika.</summary>
    [HttpGet("vendors")]
    public async Task<ActionResult<IEnumerable<ProviderVendorDto>>> MyVendors(CancellationToken ct)
    {
        var uid = Uid();

        // Pružatelji koje korisnik posjeduje (odobren) ILI ima ne-odbijeni claim.
        var claimVendorIds = await _db.Claims.AsNoTracking()
            .Where(c => c.UserId == uid).Select(c => c.VendorId).ToListAsync(ct);
        var owned = await _db.Vendors.AsNoTracking()
            .Where(v => v.OwnerUserId == uid).Select(v => v.Id).ToListAsync(ct);
        var ids = claimVendorIds.Concat(owned).Distinct().ToList();
        if (ids.Count == 0) return Ok(Array.Empty<ProviderVendorDto>());

        var vendors = await _db.Vendors.AsNoTracking().Where(v => ids.Contains(v.Id)).ToListAsync(ct);
        var claims = await _db.Claims.AsNoTracking().Where(c => c.UserId == uid && ids.Contains(c.VendorId)).ToListAsync(ct);
        var drafts = await _db.VendorDrafts.AsNoTracking().Where(d => ids.Contains(d.VendorId)).ToListAsync(ct);
        var photos = await _db.Set<VendorPhoto>().AsNoTracking().Where(p => ids.Contains(p.VendorId)).ToListAsync(ct);

        var list = new List<ProviderVendorDto>(vendors.Count);
        foreach (var v in vendors)
        {
            var claim = claims.FirstOrDefault(c => c.VendorId == v.Id);
            var isOwner = v.OwnerUserId == uid && v.ClaimStatus == "claimed";
            var myStatus = isOwner ? "owner" : (claim?.Status ?? "pending");
            var draft = drafts.FirstOrDefault(d => d.VendorId == v.Id) ?? ProviderMapper.SeedFromVendor(v);
            var vPhotos = photos.Where(p => p.VendorId == v.Id).OrderBy(p => p.SortOrder)
                .Select(ProviderMapper.PhotoDto).ToList();
            list.Add(new ProviderVendorDto(
                Slug: v.Slug, Name: v.Name, Category: v.CategorySlug,
                MyStatus: myStatus, ClaimStatus: v.ClaimStatus, CanPublish: isOwner,
                Draft: ProviderMapper.ToDto(draft),
                Stats: await StatsFor(v.Slug, ct),
                Photos: vPhotos));
        }
        return Ok(list.OrderByDescending(x => x.MyStatus == "pending").ToList());
    }

    /// <summary>PUT /api/provider/vendors/{slug}/draft — spremi draft (pending ili vlasnik).</summary>
    [HttpPut("vendors/{slug}/draft")]
    public async Task<IActionResult> SaveDraft(string slug, [FromBody] VendorDraftDto dto, CancellationToken ct)
    {
        var uid = Uid();
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Slug == slug, ct);
        if (vendor == null) return NotFound();
        if (!await CanEdit(vendor, uid, ct)) return Forbid();

        var draft = await _db.VendorDrafts.FirstOrDefaultAsync(d => d.VendorId == vendor.Id, ct);
        if (draft == null) { draft = ProviderMapper.SeedFromVendor(vendor); _db.VendorDrafts.Add(draft); }

        var err = ProviderMapper.ApplyDto(draft, dto);
        if (err != null) return BadRequest(new { error = err });

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// POST /api/provider/vendors/{slug}/publish — objavi draft u živu verziju.
    /// Dozvoljeno SAMO odobrenom vlasniku; pending pružatelj to ne može (objava ide preko admina).
    /// </summary>
    [HttpPost("vendors/{slug}/publish")]
    public async Task<IActionResult> Publish(string slug, CancellationToken ct)
    {
        var uid = Uid();
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Slug == slug, ct);
        if (vendor == null) return NotFound();
        if (!(vendor.OwnerUserId == uid && vendor.ClaimStatus == "claimed"))
            return Forbid();

        var draft = await _db.VendorDrafts.FirstOrDefaultAsync(d => d.VendorId == vendor.Id, ct);
        if (draft == null) return BadRequest(new { error = "no_draft" });

        ProviderMapper.ApplyToVendor(draft, vendor);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Uređivati smije odobreni vlasnik ILI korisnik s pending claimom nad pružateljem.</summary>
    private async Task<bool> CanEdit(Vendor v, Guid uid, CancellationToken ct)
    {
        if (v.OwnerUserId == uid && v.ClaimStatus == "claimed") return true;
        return await _db.Claims.AnyAsync(c => c.VendorId == v.Id && c.UserId == uid && c.Status == "pending", ct);
    }

    /// <summary>Osnovna statistika iz daily_stats (agregirano; §A). Zadnjih 30 dana.</summary>
    private async Task<ProviderStatsDto> StatsFor(string slug, CancellationToken ct)
    {
        var since = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var rows = await _db.DailyStats.AsNoTracking()
            .Where(s => s.VendorSlug == slug && s.Day >= since)
            .GroupBy(s => s.EventName)
            .Select(g => new { Event = g.Key, Total = g.Sum(x => x.Count) })
            .ToListAsync(ct);
        int Get(string e) => rows.FirstOrDefault(r => r.Event == e)?.Total ?? 0;
        return new ProviderStatsDto(Get("vendor_viewed"), Get("compare_added"), Get("favorite_added"));
    }
}
