using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Contracts;
using Wediplan.Api.Data;

namespace Wediplan.Api.Controllers;

/// <summary>
/// GET /api/pins?category=&amp;region=&amp;q= — pinovi za kartu (Faza 2, §L odluka d).
/// Category je OBAVEZNA (category-first; nikad "svi pružatelji" — §8). Cap 1000.
/// Vraća samo pružatelje s koordinatama; jitter oko centroida radi frontend (lib/jitter.ts),
/// pa API uvijek vraća istinite (negeneralizirane) koordinate.
/// </summary>
[ApiController]
[Route("api/pins")]
[EnableRateLimiting("lists")] // §8
public class PinsController : ControllerBase
{
    private const int MaxPins = 1000;

    private readonly AppDbContext _db;
    public PinsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PinsResultDto>> Get(
        [FromQuery] string? category,
        [FromQuery] string? region,
        [FromQuery] string? q,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(category) || !Catalog.CategoryBySlug.ContainsKey(category))
            return BadRequest(new { error = "category_required" });

        var query = _db.Vendors.AsNoTracking()
            .Published()
            .InRegion(region)
            .InCategory(category)
            .MatchText(q)
            .Where(v => v.Lat != null && v.Lng != null);

        var total = await query.CountAsync(ct);

        // Projekcija samo potrebnih stupaca (bez about/services/kontakata)
        var rows = await query
            .Ranked()
            .Take(MaxPins)
            .Select(v => new
            {
                v.Id, v.Slug, v.Name, v.CategorySlug, v.City, v.Lng, v.Lat, v.LocationPrecision,
                v.PriceKind, v.PriceFrom, v.PriceTo, v.Rating, v.ReviewCount,
                Cats = v.Categories.OrderByDescending(c => c.IsPrimary).Select(c => c.CategorySlug).ToList(),
                Cover = v.Photos.OrderBy(p => p.SortOrder).Select(p => p.StorageKey).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new PinDto(
            Id: r.Id.ToString(),
            Slug: r.Slug,
            Name: r.Name,
            Category: r.CategorySlug,
            City: r.City,
            Lng: r.Lng!.Value,
            Lat: r.Lat!.Value,
            Price: VendorMapper.Price(r.PriceKind, r.PriceFrom, r.PriceTo),
            Rating: r.Rating,
            ReviewCount: r.ReviewCount,
            Categories: r.Cats.Count > 1 ? r.Cats : null,
            LocationPrecision: r.LocationPrecision,
            Photos: r.Cover != null ? new[] { r.Cover } : null)).ToList();

        return Ok(new PinsResultDto(items, total));
    }
}
