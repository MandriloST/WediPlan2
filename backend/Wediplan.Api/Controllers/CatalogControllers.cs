using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Contracts;
using Wediplan.Api.Data;

namespace Wediplan.Api.Controllers;

/// <summary>
/// GET /api/regions?category= — regije s brojačima.
/// Faza 2: broji po PRAVILU LISTE (sjedište ∪ pokrivanje; zbroj > ukupno je očekivan), tako da
/// broj uz regiju odgovara broju rezultata nakon klika. Opcionalni category sužava na kategoriju.
/// </summary>
[ApiController]
[Route("api/regions")]
public class RegionsController : ControllerBase
{
    private readonly AppDbContext _db;
    public RegionsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RegionDto>>> Get(
        [FromQuery] string? category, CancellationToken ct)
    {
        var baseQ = _db.Vendors.AsNoTracking().Published().InCategory(category);

        // 5 malih COUNT upita (5 regija) — jednostavno i indeksirano; keš je u Next data cacheu
        var result = new List<RegionDto>(Catalog.Regions.Count);
        foreach (var r in Catalog.Regions)
        {
            var n = await baseQ.InRegion(r.Id).CountAsync(ct);
            result.Add(new RegionDto(r.Id, r.Name, r.Center, r.Bounds, n));
        }
        return Ok(result);
    }
}

/// <summary>
/// GET /api/categories?region= — kategorije s brojačima po SVIM kategorijama (§4.3, §L).
/// Zbroj brojača > broj pružatelja je očekivan.
/// </summary>
[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    public CategoriesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> Get(
        [FromQuery] string? region, CancellationToken ct)
    {
        var q = _db.VendorCategories
            .Where(vc => vc.Vendor.IsPublished && !vc.Vendor.OptOut);

        if (!string.IsNullOrWhiteSpace(region))
            q = q.Where(vc =>
                vc.Vendor.RegionSlug == region ||
                vc.Vendor.CoverageAll ||
                vc.Vendor.CoverageRegions.Contains(region));

        var counts = await q
            .GroupBy(vc => vc.CategorySlug)
            .Select(g => new { Slug = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Slug, x => x.Count, ct);

        var result = Catalog.Categories.Select(c => new CategoryDto(
            c.Slug, c.Name, c.Group, counts.GetValueOrDefault(c.Slug, 0), c.Short));
        return Ok(result);
    }
}

/// <summary>GET /api/budget-defaults?region= — regionalna raspodjela (lib/budget.ts).</summary>
[ApiController]
[Route("api/budget-defaults")]
public class BudgetDefaultsController : ControllerBase
{
    [HttpGet]
    public ActionResult<BudgetDefaultsDto> Get([FromQuery] string? region)
        => Ok(new BudgetDefaultsDto(region ?? "hr", Catalog.SharesFor(region)));
}

/// <summary>
/// GET /api/budget-matches?region=&amp;guests=&amp;sala=&amp;catering=&amp;foto=&amp;glazba=&amp;ostalo=
/// Koliko pružatelja u regiji stane u capove plana — po budžetskoj grupi (primarna kategorija)
/// i ukupno. Klijent šalje capove koje je sam izračunao (lib/budget computeCaps), pa pravilo
/// zaokruživanja postoji samo na jednom mjestu. "na upit" = 0 € (ne isključuje se iz budžeta).
/// </summary>
[ApiController]
[Route("api/budget-matches")]
public class BudgetMatchesController : ControllerBase
{
    private static readonly string[] Groups = { "sala", "catering", "foto", "glazba", "ostalo" };

    private readonly AppDbContext _db;
    public BudgetMatchesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<BudgetMatchesDto>> Get(
        [FromQuery] string? region, [FromQuery] int guests, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(region) || !Catalog.RegionById.ContainsKey(region))
            return BadRequest(new { error = "region_required" });

        guests = Math.Clamp(guests, 0, 5000);
        var caps = Groups.ToDictionary(g => g, g =>
            long.TryParse(Request.Query[g], out var c) ? Math.Max(0, c) : 0L);

        // Minimalna projekcija (≤ ~3500 redaka × 3 stupca) → izračun u memoriji
        var rows = await _db.Vendors.AsNoTracking().Published().InRegion(region)
            .Select(v => new { v.CategorySlug, v.PriceKind, v.PriceFrom })
            .ToListAsync(ct);

        var counts = Groups.ToDictionary(g => g, _ => 0);
        foreach (var r in rows)
        {
            var group = Catalog.CategoryBySlug.TryGetValue(r.CategorySlug, out var cat) ? cat.Group : "ostalo";
            if (!counts.ContainsKey(group)) group = "ostalo";
            long cost = r.PriceKind switch
            {
                "from" => r.PriceFrom ?? 0,
                "perPerson" => (long)(r.PriceFrom ?? 0) * guests,
                _ => 0, // na upit
            };
            if (cost <= caps[group]) counts[group]++;
        }

        return Ok(new BudgetMatchesDto(region, counts.Values.Sum(), counts));
    }
}

/// <summary>
/// GET /api/sitemap — slugovi objavljenih pružatelja + zadnja izmjena, za Next.js sitemap.xml.
/// Namjerno BEZ ikakvih drugih podataka (sitemap je ionako javan). robots.txt skriva /api/.
/// </summary>
[ApiController]
[Route("api/sitemap")]
public class SitemapController : ControllerBase
{
    private readonly AppDbContext _db;
    public SitemapController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SitemapEntryDto>>> Get(CancellationToken ct)
    {
        var rows = await _db.Vendors.AsNoTracking().Published()
            .OrderBy(v => v.Slug)
            .Take(45_000) // sitemap limit je 50k URL-ova po datoteci (ostatak su kategorije/regije)
            .Select(v => new { v.Slug, v.UpdatedAt })
            .ToListAsync(ct);
        return Ok(rows.Select(r => new SitemapEntryDto(r.Slug, r.UpdatedAt)));
    }
}

/// <summary>
/// GET /api/suggest?q= — typeahead. Kategorije/regije iz šifrarnika (statično),
/// gradovi/pružatelji iz baze preko pg_trgm ILIKE. Mirror lib/search.ts suggest().
/// </summary>
[ApiController]
[Route("api/suggest")]
[EnableRateLimiting("lists")] // §8
public class SuggestController : ControllerBase
{
    private readonly AppDbContext _db;
    public SuggestController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SuggestItemDto>>> Get(
        [FromQuery] string q, CancellationToken ct)
    {
        var input = (q ?? "").Trim();
        if (input.Length == 0) return Ok(Array.Empty<SuggestItemDto>());

        if (input.Length > 80) input = input[..80];
        var like = "%" + input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        var outp = new List<SuggestItemDto>();

        foreach (var c in Catalog.Categories)
            if (NormContains(c.Name, input) || (c.Short != null && NormContains(c.Short, input)))
                outp.Add(new SuggestItemDto("category", c.Name, $"/{c.Slug}", "kategorija"));

        foreach (var r in Catalog.Regions)
            if (NormContains(r.Name, input))
                outp.Add(new SuggestItemDto("region", r.Name, $"/{r.Id}", "regija"));

        var cities = await _db.Vendors
            .Published()
            .Where(v => v.City != "" && EF.Functions.ILike(v.City, like))
            .Select(v => v.City).Distinct().Take(5).ToListAsync(ct);
        foreach (var city in cities)
            outp.Add(new SuggestItemDto("city", city, $"/?q={Uri.EscapeDataString(city)}", "grad"));

        var vendors = await _db.Vendors
            .Published()
            .Where(v => EF.Functions.ILike(v.Name, like))
            .OrderByDescending(v => v.Rating)
            .Select(v => new { v.Name, v.City, v.CategorySlug, v.Slug })
            .Take(7).ToListAsync(ct);
        foreach (var v in vendors)
        {
            var cat = Catalog.CategoryBySlug.GetValueOrDefault(v.CategorySlug);
            var sub = string.Join(" · ", new[] { cat?.Short ?? cat?.Name, v.City }
                .Where(x => !string.IsNullOrEmpty(x)));
            // Faza 2: pružatelj iz typeaheada vodi ravno na profil
            outp.Add(new SuggestItemDto("vendor", v.Name, $"/pruzatelj/{Uri.EscapeDataString(v.Slug)}", sub));
        }

        return Ok(outp.Take(7));
    }

    private static bool NormContains(string h, string n) => TextNorm.NormContains(h, n);
}
