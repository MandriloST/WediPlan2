using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Contracts;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Controllers;

/// <summary>
/// Couple podaci (Faza 3 kraj, §5 "favoriti/plan sinkronizirani"): favoriti i budžetski plan
/// prijavljenog korisnika. Sve rute traže sesiju ([Authorize]). Favoriti se vraćaju kao lista
/// ID-jeva (frontend njima dohvaća pune Vendore preko /api/vendors?ids=, isti put kao usporedba).
/// </summary>
[ApiController]
[Route("api/favorites")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private const int MaxFavorites = 200; // razuman gornji limit po korisniku

    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    public FavoritesController(AppDbContext db, UserManager<AppUser> users) { _db = db; _users = users; }

    private Guid Uid() => Guid.Parse(_users.GetUserId(User)!);

    /// <summary>GET /api/favorites — ID-jevi favorita + plan (couple podaci u jednom pozivu).</summary>
    [HttpGet]
    public async Task<ActionResult<CoupleDataDto>> Get(CancellationToken ct)
    {
        var uid = Uid();
        var favIds = await _db.Favorites.AsNoTracking()
            .Where(f => f.UserId == uid)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.VendorId.ToString())
            .ToListAsync(ct);
        var plan = await _db.BudgetPlans.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == uid, ct);
        return Ok(new CoupleDataDto(favIds, ToDto(plan)));
    }

    /// <summary>PUT /api/favorites/{vendorId} — dodaj favorit (idempotentno).</summary>
    [HttpPut("{vendorId:guid}")]
    public async Task<IActionResult> Add(Guid vendorId, CancellationToken ct)
    {
        var uid = Uid();
        var count = await _db.Favorites.CountAsync(f => f.UserId == uid, ct);
        var exists = await _db.Favorites.AnyAsync(f => f.UserId == uid && f.VendorId == vendorId, ct);
        if (!exists)
        {
            if (count >= MaxFavorites) return StatusCode(409, new { error = "too_many_favorites" });
            _db.Favorites.Add(new Favorite { UserId = uid, VendorId = vendorId });
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    /// <summary>DELETE /api/favorites/{vendorId} — makni favorit (idempotentno).</summary>
    [HttpDelete("{vendorId:guid}")]
    public async Task<IActionResult> Remove(Guid vendorId, CancellationToken ct)
    {
        var uid = Uid();
        await _db.Favorites.Where(f => f.UserId == uid && f.VendorId == vendorId).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// POST /api/favorites/merge — spoji localStorage favorite/plan u account nakon prijave.
    /// Favoriti: UNIJA (postojeći + novi, bez duplikata). Plan: postavlja se SAMO ako korisnik
    /// još nema plan (ne pregazi postojeći — §5 "merge, ne pregazi"). Vraća spojeno stanje.
    /// </summary>
    [HttpPost("merge")]
    public async Task<ActionResult<CoupleDataDto>> Merge([FromBody] MergeRequest req, CancellationToken ct)
    {
        var uid = Uid();

        if (req.FavoriteIds is { Count: > 0 })
        {
            var incoming = req.FavoriteIds
                .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue).Select(g => g!.Value)
                .Distinct().Take(MaxFavorites).ToList();

            var existing = await _db.Favorites.Where(f => f.UserId == uid)
                .Select(f => f.VendorId).ToListAsync(ct);
            var have = existing.ToHashSet();
            var room = MaxFavorites - have.Count;

            foreach (var vid in incoming)
            {
                if (room <= 0) break;
                if (have.Add(vid))
                {
                    _db.Favorites.Add(new Favorite { UserId = uid, VendorId = vid });
                    room--;
                }
            }
        }

        if (req.Plan is { } p && (p.Total > 0 || p.Guests > 0))
        {
            var existingPlan = await _db.BudgetPlans.FirstOrDefaultAsync(x => x.UserId == uid, ct);
            if (existingPlan == null) // ne pregazi ako korisnik već ima plan
            {
                _db.BudgetPlans.Add(new Domain.BudgetPlan
                {
                    UserId = uid, Guests = p.Guests, Region = p.Region ?? "", Total = p.Total,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return await Get(ct);
    }

    /// <summary>PUT /api/favorites/plan — spremi/zamijeni budžetski plan (eksplicitna korisnička akcija).</summary>
    [HttpPut("plan")]
    public async Task<IActionResult> SavePlan([FromBody] BudgetPlanDto dto, CancellationToken ct)
    {
        var uid = Uid();
        var plan = await _db.BudgetPlans.FirstOrDefaultAsync(x => x.UserId == uid, ct);
        if (plan == null)
        {
            _db.BudgetPlans.Add(new Domain.BudgetPlan { UserId = uid, Guests = dto.Guests, Region = dto.Region ?? "", Total = dto.Total });
        }
        else
        {
            plan.Guests = dto.Guests; plan.Region = dto.Region ?? ""; plan.Total = dto.Total; plan.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>DELETE /api/favorites/plan — obriši plan.</summary>
    [HttpDelete("plan")]
    public async Task<IActionResult> DeletePlan(CancellationToken ct)
    {
        var uid = Uid();
        await _db.BudgetPlans.Where(x => x.UserId == uid).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    private static BudgetPlanDto? ToDto(Domain.BudgetPlan? p) =>
        p == null ? null : new BudgetPlanDto(p.Guests, p.Region, p.Total);
}
