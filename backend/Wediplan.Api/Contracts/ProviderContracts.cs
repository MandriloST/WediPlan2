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
    string Evidence,      // domain_match | email_verified | ""
    DateTime CreatedAt);

/// <summary>POST /api/claims/verify — potvrdi vlasništvo tokenom poslanim na Vendor.Email (§Zadatak 5).</summary>
public record VerifyClaimRequest([property: Required] string Token);

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
public record ProviderPhotoDto(
    string Id, string Url, string ThumbUrl, bool IsCover, int SortOrder,
    // §Zadatak 15 — status moderacije (unreviewed | approved | flagged). Vlasniku se kao razlika prikazuje samo "flagged"
    // (slika je skrivena od javnosti); ModerationNote je razlog koji je upisao admin.
    string ModerationStatus = "unreviewed", string? ModerationNote = null);

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
    DateTime CreatedAt,
    // §Zadatak 16 — evidencija odluke (null dok je recenzija pending / za odluke prije evidencije)
    DateTime? DecidedAt = null,
    string? DeciderEmail = null,
    string? RejectReason = null);

/// <summary>Neobavezno tijelo za <c>POST /api/admin/reviews/{id}/reject</c> (razlog je interni, max 500 znakova).</summary>
public record RejectReviewRequest(string? Reason);

/// <summary>
/// Uvezena recenzija ("što oni kažu") za provjeru u adminu (§Zadatak 16). <c>VerificationStatus</c>:
/// unverified | verified | rejected. <c>EvidenceNote</c> = kratka napomena o dokazu (npr. gdje je screenshot).
/// </summary>
public record AdminImportedReviewDto(
    string Id,
    string VendorSlug,
    string VendorName,
    string Author,
    int Rating,
    string Text,
    string Source,
    int Year,
    string VerificationStatus,
    DateTime? VerifiedAt,
    string? VerifierEmail,
    string? EvidenceNote);

/// <summary>
/// Fotografija pružatelja u admin redu za pregled (§Zadatak 15). Slike su javne odmah (post-moderacija); admin vodi evidenciju
/// <c>ModerationStatus</c>: unreviewed | approved | flagged (sakrivena). <c>Source</c>: partner | import.
/// </summary>
public record AdminPhotoDto(
    string Id, string VendorSlug, string VendorName, string Url, string ThumbUrl, bool IsCover,
    string Source, string ModerationStatus, DateTime? CreatedAt, DateTime? RightsConfirmedAt,
    string? UploaderEmail, DateTime? ReviewedAt, string? ReviewerEmail, string? ModerationNote);

/// <summary>Tijelo za <c>POST /api/admin/photos/{id}/flag</c>: razlog je OBAVEZAN (max 1000) i vidi ga vlasnik profila.</summary>
public record FlagPhotoRequest(string? Note);

/// <summary>Tijelo za <c>POST /api/admin/photos/approve-batch</c> (max 60 id-eva).</summary>
public record ApprovePhotosRequest(IReadOnlyList<string>? Ids);

/// <summary>Tijelo za <c>POST /api/admin/imported-reviews/{id}/verify|reject</c> (napomena o dokazu, max 1000 znakova).</summary>
public record ImportedReviewDecisionRequest(string? EvidenceNote);

// ---------------------------------------------------------------- Faza 6: GDPR opt-out (§9)
/// <summary>Zahtjev za skidanje neclaimanog profila (GDPR §9). Slug + neobavezni razlog/kontakt.</summary>
public record OptOutRequest(string Slug, string? Reason, string? Contact);

/// <summary>Skriveni (opt-out) pružatelj u admin pregledu.</summary>
public record AdminOptOutDto(string Slug, string Name, string Category, bool IsPublished);

/// <summary>
/// Redak dnevnika promjena u admin pregledu (§Zadatak 14, GDPR). <c>Changes</c> je JSON kao tekst:
/// <c>{"PriceFrom":{"old":800,"new":950}}</c>, maskirano <c>{"Phone":{"changed":true}}</c> ili novi entitet
/// <c>{"Name":"…","Phone":{"set":true}}</c>. <c>ActorEmail</c> je null za javne/sistemske radnje i za obrisane korisnike.
/// </summary>
public record AdminAuditEntryDto(
    long Id, DateTime OccurredAt, string ActorType, string? ActorEmail,
    string EntityType, string EntityId, string Action, string? Changes, string? Source);
