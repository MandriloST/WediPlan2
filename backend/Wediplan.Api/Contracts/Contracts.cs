using System.Text.Json.Serialization;

namespace Wediplan.Api.Contracts;

// ============================================================================
// Ugovor s frontendom — 1:1 zrcalo API.md i frontend lib/types.ts.
// Serializacija: camelCase + izostavljanje null polja (postavljeno u Program.cs).
// KONTAKTI (phone/email/website) se NAMJERNO ne nalaze u VendorDto (§8-§9, odluka #13).
// ============================================================================

/// <summary>Paged&lt;T&gt; iz lib/types.ts — oblik odgovora GET /api/vendors.</summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);

/// <summary>PriceModel iz lib/types.ts. kind: from | perPerson | onRequest.</summary>
public record PriceDto(
    string Kind,
    int? From = null,
    int? To = null)
{
    public static PriceDto FromPrice(int from) => new("from", from);
    public static PriceDto PerPerson(int from, int to) => new("perPerson", from, to);
    public static PriceDto OnRequest() => new("onRequest");
}

/// <summary>Društvene poveznice (javni portfolio).</summary>
public record SocialDto(string? Instagram = null, string? Facebook = null);

/// <summary>
/// Vendor iz lib/types.ts (lista/karta/profil). BEZ kontakata.
/// coverage je unija string[] | "hr": tip je object? — string "hr" ili string[].
/// </summary>
public record VendorDto(
    string Id,
    string Slug,
    string Name,
    string Category,                    // primarna (Category.slug)
    string Region,                      // RegionId sjedišta
    string City,
    double? Lng,
    double? Lat,
    PriceDto Price,
    double Rating,
    int ReviewCount,
    bool Verified,
    bool LiveCalendar,
    IReadOnlyList<string> StyleTags,
    IReadOnlyList<string>? Categories = null,     // sve kategorije (§4.3)
    string? LocationPrecision = null,             // exact | city | region
    object? Coverage = null,                       // string[] ili "hr"
    string? CoverageNote = null,
    string? RatingSource = null,
    SocialDto? Social = null,
    string? ClaimStatus = null,
    IReadOnlyList<string>? Photos = null,
    string? Country = null);          // "ba"|"si"; izostavljeno za "hr" (domaći)

/// <summary>RegionWithCount iz lib/types.ts — GET /api/regions.</summary>
public record RegionDto(
    string Id,
    string Name,
    double[] Center,
    double[][] Bounds,
    int Count);

/// <summary>GET /api/categories — kategorija s brojačem (§L, §4.3).</summary>
public record CategoryDto(
    string Slug,
    string Name,
    string Group,
    int Count,
    string? Short = null);

/// <summary>Suggestion iz lib/search.ts — GET /api/suggest.</summary>
public record SuggestItemDto(
    string Type,
    string Label,
    string Href,
    string? Sub = null);

/// <summary>GET /api/budget-defaults.</summary>
public record BudgetDefaultsDto(
    string Region,
    IReadOnlyDictionary<string, double> Shares);

/// <summary>GET /api/vendors/{slug} — profil pružatelja.</summary>
public record VendorProfileDto(
    VendorDto Vendor,
    string About,
    IReadOnlyList<string> Services,
    IReadOnlyList<ImportedReviewDto> ImportedReviews);

public record ImportedReviewDto(
    string Author,
    int Rating,
    string Text,
    string Source,
    int Year);

// ============================================================================
// Faza 2 — dopune ugovora (API.md; frontend lib/types.ts)
// ============================================================================

/// <summary>
/// GET /api/pins — minimalni oblik pina za kartu (podskup VendorDto; frontend PinVendor).
/// Bez kontakata i bez opisa. Samo pružatelji s koordinatama.
/// </summary>
public record PinDto(
    string Id,
    string Slug,
    string Name,
    string Category,
    string City,
    double Lng,
    double Lat,
    PriceDto Price,
    double Rating,
    int ReviewCount,
    IReadOnlyList<string>? Categories = null,
    string? LocationPrecision = null,
    IReadOnlyList<string>? Photos = null);   // najviše 1 (naslovna)

/// <summary>total = svi s koordinatama za upit; items može biti rezan capom.</summary>
public record PinsResultDto(IReadOnlyList<PinDto> Items, int Total);

/// <summary>GET /api/budget-matches — koliko pružatelja stane u capove (po grupi i ukupno).</summary>
public record BudgetMatchesDto(
    string Region,
    int Matches,
    IReadOnlyDictionary<string, int> Groups);

/// <summary>GET /api/sitemap — slug + zadnja izmjena (bez ikakvih drugih podataka).</summary>
public record SitemapEntryDto(string Slug, DateTime UpdatedAt);
