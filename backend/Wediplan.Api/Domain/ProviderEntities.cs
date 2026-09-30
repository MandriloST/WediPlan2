namespace Wediplan.Api.Domain;

// ============================================================================
// Faza 4 — preuzimanje profila (claim), korisničke recenzije, draft uređivanja i
// pretplate (schema sada, logika kasnije). Referenca: PLAN §3, §6, §M.4.
// Imena stupaca snake_case (konfigurirano centralno u AppDbContext).
// ============================================================================

/// <summary>
/// Zahtjev pružatelja da preuzme SVOJ profil (§6). Ide u <c>pending</c>; pružatelj ODMAH dobiva
/// pristup uređivanju DRAFTA, ali javni profil se ne mijenja dok admin ne odobri (§6.3).
/// <para><c>Evidence = "domain_match"</c> kad se domena korisnikova e-maila poklapa s web-domenom
/// iz profila — takav claim admin može brže odobriti; inače <c>""</c> (samo poruka).</para>
/// </summary>
public class Claim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendorId { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; } = "";

    /// <summary>"domain_match" | "email_verified" | "" — dokaz vlasništva. "domain_match" se izvodi
    /// pri kreiranju; "email_verified" nakon klika na poveznicu poslanu na Vendor.Email (§Zadatak 5).</summary>
    public string Evidence { get; set; } = "";

    /// <summary>pending | approved | rejected.</summary>
    public string Status { get; set; } = "pending";

    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Jednokratni token za dokaz vlasništva claima (§ Plan prioriteti 2, Zadatak 5). Šalje se na
/// <c>Vendor.Email</c> (adresa iz importa, javno se ne izlaže) — tko klikne link iz TOG inboxa,
/// dokazao je kontrolu nad službenom adresom profila. Isti obrazac kao <c>EmailVerificationToken</c>:
/// sirovi token u mailu, u bazu SAMO SHA-256 hash.
/// </summary>
public class ClaimVerificationToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClaimId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}

/// <summary>
/// "Što korisnici kažu" (§6, feature #4 — dvojaka recenzija). Registrirani korisnik ostavlja
/// ocjenu + tekst; ide u <c>pending</c> (moderacija) prije objave. Jedna recenzija po
/// (korisnik, pružatelj) — jedinstveni indeks to jamči.
/// </summary>
public class UserReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendorId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }          // 1–5
    public string Text { get; set; } = "";

    /// <summary>pending | published | rejected.</summary>
    public string Status { get; set; } = "pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
}

/// <summary>
/// Draft uređivanja profila (§6.3): izmjene pružatelja NISU javne dok se ne objave — admin pri
/// odobrenju claima, ili sam vlasnik nakon što je claim odobren. Jedan draft po pružatelju.
/// <para>Fotografije (Faza 5, R2) i kategorije (§4.3, limit) se OVDJE ne uređuju — samo
/// tekstualni/cjenovni dio koji pružatelj sam mijenja u ovoj fazi.</para>
/// </summary>
public class VendorDraft
{
    public Guid VendorId { get; set; }        // PK = 1:1 s pružateljem
    public string? About { get; set; }
    public List<string> Services { get; set; } = new();

    // Cijena (isti model kao Vendor): kind = from | perPerson | onRequest
    public string PriceKind { get; set; } = "onRequest";
    public int? PriceFrom { get; set; }
    public int? PriceTo { get; set; }

    public List<string> StyleTags { get; set; } = new();
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Pretplata (§M.4) — schema sada, logika (freemium granica / founding partner) kasnije.
/// Prazna u Fazi 4; premium se čita iz ovoga, nikad hardkodirano po pružatelju.
/// </summary>
public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendorId { get; set; }
    public string Plan { get; set; } = "free"; // free | premium | founding
    public DateTime? ActiveFrom { get; set; }
    public DateTime? ActiveTo { get; set; }
    public Guid? GrantedBy { get; set; }        // za founding (admin dodjeljuje)
}
