using System.ComponentModel.DataAnnotations;

namespace Wediplan.Api.Contracts;

// ============================================================================
// Faza 4 — ugovor za claim, korisničke recenzije, provider dashboard i admin.
// Zrcalo: frontend lib/types.ts (Faza 4) i API.md. camelCase + izostavljanje null.
// ============================================================================

// ---------------------------------------------------------------- korisničke recenzije

/// <summary>Objavljena recenzija korisnika (javno, na profilu). Autor = displayName ili generički.</summary>
public record UserReviewDto(
    string Id,
    string Author,
    int Rating,
    string Text,
    DateTime CreatedAt);

/// <summary>POST /api/reviews — nova recenzija (auth). Ide u moderaciju (pending).</summary>
public record ReviewRequest(
    [property: Required] string VendorSlug,
    [property: Range(1, 5)] int Rating,
    [property: Required, MinLength(10), MaxLength(4000)] string Text);

// ---------------------------------------------------------------- claim (preuzimanje profila)

/// <summary>POST /api/claims — zahtjev za preuzimanje profila (auth).</summary>
public record ClaimRequest(
    [property: Required] string VendorSlug,
    [property: MaxLength(2000)] string? Message);

/// <summary>Claim iz perspektive korisnika (GET /api/claims/mine).</summary>
public record ClaimDto(
    string Id,
    string VendorSlug,
    string VendorName,
    string Status,        // pending | approved | rejected
    string Evidence,      // domain_match | ""
    DateTime CreatedAt);

// ---------------------------------------------------------------- provider dashboard

/// <summary>Draft uređivanja (ulaz i izlaz). price je isti oblik kao VendorDto.Price.</summary>
public record VendorDraftDto(
    string? About,
    IReadOnlyList<string> Services,
    PriceDto Price,
    IReadOnlyList<string> StyleTags);

/// <summary>Osnovna statistika profila (§6.5, §M.1 free): pregledi i dodavanja u usporedbu, 30 dana.</summary>
public record ProviderStatsDto(int Views30, int Compares30, int Favorites30);

/// <summary>Fotografija pružatelja (Faza 5). thumbUrl je izveden iz url-a konvencijom (_thumb).</summary>
public record ProviderPhotoDto(string Id, string Url, string ThumbUrl, bool IsCover, int SortOrder);

/// <summary>PUT …/photos/order — novi poredak + naslovna.</summary>
public record PhotoOrderRequest(IReadOnlyList<string> OrderedIds, string? CoverId);

/// <summary>
/// Jedan pružatelj u nadzornoj ploči partnera. myStatus opisuje odnos ovog korisnika:
/// pending (čeka odobrenje), owner (odobren vlasnik), rejected.
/// </summary>
public record ProviderVendorDto(
    string Slug,
    string Name,
    string Category,
    string MyStatus,      // pending | owner | rejected
    string ClaimStatus,   // vendor.claim_status: unclaimed | claimed
    bool CanPublish,      // true samo za odobrenog vlasnika (pending ide preko admina)
    VendorDraftDto Draft,
    ProviderStatsDto Stats,
    IReadOnlyList<ProviderPhotoDto> Photos);

// ---------------------------------------------------------------- admin (moderacija)

public record AdminClaimDto(
    string Id,
    string VendorSlug,
    string VendorName,
    string UserEmail,
    string? UserDisplayName,
    string Message,
    string Evidence,
    string Status,
    DateTime CreatedAt);

public record AdminReviewDto(
    string Id,
    string VendorSlug,
    string VendorName,
    string UserEmail,
    int Rating,
    string Text,
    string Status,
    DateTime CreatedAt);

// ---------------------------------------------------------------- Faza 6: GDPR opt-out (§9)
/// <summary>Zahtjev za skidanje neclaimanog profila (GDPR §9). Slug + neobavezni razlog/kontakt.</summary>
public record OptOutRequest(string Slug, string? Reason, string? Contact);

/// <summary>Skriveni (opt-out) pružatelj u admin pregledu.</summary>
public record AdminOptOutDto(string Slug, string Name, string Category, bool IsPublished);
