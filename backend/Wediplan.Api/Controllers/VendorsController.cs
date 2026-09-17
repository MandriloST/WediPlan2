using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Contracts;
using Wediplan.Api.Data;

namespace Wediplan.Api.Controllers;

/// <summary>
/// GET /api/vendors (lista, filtri, ?ids=) i GET /api/vendors/{slug} (profil). Ugovor: API.md.
/// Category-first (§L): frontend ne zove listu bez category/q, ali ugovor to dopušta (SEO).
/// Kontakti se NE vraćaju (§8-§9, odluka #13).
/// </summary>
[ApiController]
[Route("api/vendors")]
public class VendorsController : ControllerBase
{
    private const int DefaultPageSize = 24; // §L
    private const int MaxPageSize = 50;     // anti-scraping (§8)
    private const int MaxIds = 50;          // usporedba/favoriti (Faza 2)

    private readonly AppDbContext _db;
    public VendorsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedResult<VendorDto>>> List(
        [FromQuery] string? q,
        [FromQuery] string? region,
        [FromQuery] string? category,
        [FromQuery] string? date,       // rezervirano (dostupnost) — ne filtrira još
        [FromQuery] string? ids,        // "uuid,uuid,…" (max 50) — usporedba i favoriti
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        // --- ?ids= : točno ti pružatelji (ostali filtri se ignoriraju); nepoznati/skriveni se izostavljaju
        if (ids != null)
        {
            var guids = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(MaxIds)
                .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue).Select(g => g!.Value)
                .Distinct().ToList();

            var found = guids.Count == 0
                ? new List<Domain.Vendor>()
                : await _db.Vendors.AsNoTracking().Published()
                    .Where(v => guids.Contains(v.Id))
                    .Include(v => v.Categories)
                    .Include(v => v.Photos)
                    .ToListAsync(ct);

            var dtos = found.Select(VendorMapper.ToDto).ToList();
            return Ok(new PagedResult<VendorDto>(dtos, dtos.Count, 1, dtos.Count));
        }

        page = Math.Clamp(page, 1, 10_000);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Vendors.AsNoTracking()
            .Published()
            .InRegion(region)
            .InCategory(category)
            .MatchText(q);

        var total = await query.CountAsync(ct);

        var items = await query
            .Ranked()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(v => v.Categories)
            .Include(v => v.Photos)
            .ToListAsync(ct);

        return Ok(new PagedResult<VendorDto>(
            items.Select(VendorMapper.ToDto).ToList(), total, page, pageSize));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<VendorProfileDto>> Get(string slug, CancellationToken ct)
    {
        var v = await _db.Vendors
            .AsNoTracking()
            .Published()
            .Where(x => x.Slug == slug)
            .Include(x => x.Categories)
            .Include(x => x.Photos)
            .Include(x => x.ImportedReviews)
            .FirstOrDefaultAsync(ct);

        if (v == null) return NotFound();

        var reviews = v.ImportedReviews
            .OrderByDescending(r => r.Year)
            .Select(r => new ImportedReviewDto(r.Author, r.Rating, r.Text, r.Source, r.Year))
            .ToList();

        // Objavljene korisničke recenzije ("što korisnici kažu", Faza 4). Autor = displayName ili generički.
        var userReviews = await (
            from ur in _db.UserReviews.AsNoTracking().Where(x => x.VendorId == v.Id && x.Status == "published")
            join u in _db.Users.AsNoTracking() on ur.UserId equals u.Id
            orderby ur.CreatedAt descending
            select new UserReviewDto(
                ur.Id.ToString(),
                u.DisplayName != null && u.DisplayName != "" ? u.DisplayName : "Korisnik Wediplana",
                ur.Rating, ur.Text, ur.CreatedAt)
        ).ToListAsync(ct);

        // about "" / services [] kad nisu uneseni — frontend (lib/profile withProfileDefaults)
        // tada prikazuje zadani tekst kategorije.
        return Ok(new VendorProfileDto(
            Vendor: VendorMapper.ToDto(v),
            About: v.About ?? "",
            Services: v.Services,
            ImportedReviews: reviews,
            UserReviews: userReviews.Count > 0 ? userReviews : null));
    }
}
