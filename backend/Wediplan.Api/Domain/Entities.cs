using System.ComponentModel.DataAnnotations;
using NpgsqlTypes;

namespace Wediplan.Api.Domain;

// ============================================================================
// Entiteti za Fazu 1. Auth/claim/favorites/reviews korisnika dolaze u Fazi 3/4
// (zasebne migracije) — ovdje su samo tablice potrebne za read-path + analitiku.
// Sadržaj hrvatski, imena stupaca snake_case (konfigurirano u AppDbContext).
// ============================================================================

public class Vendor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stabilan javni ključ (URL/SEO/import idempotencija). Unique.</summary>
    public string Slug { get; set; } = default!;
    public string Name { get; set; } = default!;

    /// <summary>Primarna kategorija (denormalizirano radi jednostavnih upita; §4.3).</summary>
    public string CategorySlug { get; set; } = default!;
    /// <summary>Regija SJEDIŠTA. PRAZNA ("") za inozemne pružatelje (Country != "hr").</summary>
    public string RegionSlug { get; set; } = default!;

    /// <summary>Država sjedišta: "hr" (default) | "ba" (BiH) | "si" (Slovenija).
    /// Inozemni (uglavnom BiH fotografi s pokrivanjem HR) nemaju HR regiju sjedišta;
    /// pronalaze se preko coverage-a, pin na karti ide po koordinatama.</summary>
    public string Country { get; set; } = "hr";
    /// <summary>Grad sjedišta; "" = poznata samo regija.</summary>
    public string City { get; set; } = "";

    /// <summary>null = koordinate nepoznate → bez pina na karti.</summary>
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    /// <summary>exact | city | region (§4.1). city ⇒ centroid grada (frontend radi jitter).</summary>
    public string LocationPrecision { get; set; } = "region";

    /// <summary>Pokrivanje: regije u kojima radi uz sjedište.</summary>
    public List<string> CoverageRegions { get; set; } = new();
    /// <summary>true = pokriva cijelu HR (DTO to serijalizira kao coverage:"hr").</summary>
    public bool CoverageAll { get; set; }
    public string? CoverageNote { get; set; }

    // Cijena (PriceModel): kind = from | perPerson | onRequest
    public string PriceKind { get; set; } = "onRequest";
    public int? PriceFrom { get; set; }
    public int? PriceTo { get; set; }

    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public string? RatingSource { get; set; }

    public bool Verified { get; set; }
    public bool LiveCalendar { get; set; }
    public List<string> StyleTags { get; set; } = new();

    public string? About { get; set; }
    public List<string> Services { get; set; } = new();

    // KONTAKTI — uvoze se, ali se NIKAD ne serijaliziraju u javni /api/vendors (§8-§9, odluka #13).
    public string? Website { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? SocialInstagram { get; set; }
    public string? SocialFacebook { get; set; }

    /// <summary>unclaimed | claimed (Faza 4). Importi ostavljaju unclaimed.</summary>
    public string ClaimStatus { get; set; } = "unclaimed";
    public Guid? OwnerUserId { get; set; } // FK dolazi u Fazi 3
    public bool IsPublished { get; set; } = true;
    /// <summary>GDPR opt-out (§9) — true skida profil iz javnog prikaza.</summary>
    public bool OptOut { get; set; }

    /// <summary>Generirani tsvector (§2.2) — puni ga baza; ne postavljati iz koda.</summary>
    public NpgsqlTsVector? Search { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Sve kategorije (primarna + dodatne) — M2M (§4.3).
    public List<VendorCategory> Categories { get; set; } = new();
    public List<VendorPhoto> Photos { get; set; } = new();
    public List<ImportedReview> ImportedReviews { get; set; } = new();
}

/// <summary>M2M kategorije (§4.3). is_primary duplira Vendor.CategorySlug radi konzistencije.</summary>
public class VendorCategory
{
    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = default!;
    public string CategorySlug { get; set; } = default!;
    public bool IsPrimary { get; set; }
}

public class VendorPhoto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = default!;
    public string StorageKey { get; set; } = default!;
    public int SortOrder { get; set; }
    public bool IsCover { get; set; }
}

/// <summary>"Što oni kažu" — prenesene recenzije (feature #4).</summary>
public class ImportedReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = default!;
    public string Author { get; set; } = default!;
    public int Rating { get; set; }
    public string Text { get; set; } = default!;
    public string Source { get; set; } = default!;
    public int Year { get; set; }
}

/// <summary>Sirovi event (§A). Bez PII/IP. Rollup → DailyStat, reporti čitaju samo DailyStat.</summary>
public class Event
{
    public long Id { get; set; }
    public DateTime Ts { get; set; } = DateTime.UtcNow;
    public string EventName { get; set; } = default!;
    public string? SessionHash { get; set; }
    public string? Page { get; set; }
    public string? Props { get; set; } // jsonb (string; serijalizacija u B2)
}

/// <summary>Dnevni agregat (§A) — jedini izvor za reporte.</summary>
public class DailyStat
{
    public DateOnly Day { get; set; }
    public string EventName { get; set; } = default!;
    public string CategorySlug { get; set; } = "";
    public string RegionSlug { get; set; } = "";
    public string VendorSlug { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>Sponzorstva/oznake (§M.4) — schema sada, logika kasnije. Prazna u Fazi 1.</summary>
public class Sponsorship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendorId { get; set; }
    public string Kind { get; set; } = default!; // npr. "featured" | "founding"
    public string? Scope { get; set; }           // npr. region/category slug
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
}
