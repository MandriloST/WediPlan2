using Microsoft.AspNetCore.Identity;

namespace Wediplan.Api.Domain;

/// <summary>
/// Korisnik (ASP.NET Core Identity nad Guid ključem — §5).
///
/// Jedan korisnik može istovremeno biti "couple" (favoriti/plan) i "provider" (uređivanje
/// pružatelja). Role se drže u Identity role tablici; ovdje su samo profilna polja.
/// Lozinka je opcionalna: magic-link i Google korisnici je nemaju (PasswordHash == null).
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    /// <summary>Prikazno ime (opcionalno; iz Googlea ili upisano). Nikad se ne koristi kao PII u analitici.</summary>
    public string? DisplayName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Zadnja uspješna prijava (za "sumnjiva aktivnost" i čišćenje neaktivnih).</summary>
    public DateTime? LastLoginAt { get; set; }
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() { }
    public AppRole(string name) : base(name) { }
}

/// <summary>Kanonske role (§5). couple je default svakom novom korisniku.</summary>
public static class Roles
{
    public const string Couple = "couple";
    public const string Provider = "provider";
    public const string Admin = "admin";
    public static readonly string[] All = { Couple, Provider, Admin };
}

/// <summary>
/// Passwordless magic link (§5, način 2). Šalje se hash tokena — u bazi NIKAD sirovi token
/// (ista logika kao lozinka: procuri li baza, linkovi se ne mogu iskoristiti). Vrijedi 15 min,
/// jednokratan (ConsumedAt).
/// </summary>
public class MagicLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    /// <summary>SHA-256 tokena (base64url sirovog tokena ide u email/URL).</summary>
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    /// <summary>IP koji je zatražio link (rate-limit i zloupotreba; ne prikazuje se korisniku).</summary>
    public string? RequestIp { get; set; }
}

/// <summary>
/// Token za potvrdu emaila kod registracije lozinkom (§5, način 3). Isti obrazac kao MagicLink:
/// hash u bazi, 24 h rok, jednokratan. Reset lozinke koristi Identityjev ugrađeni token provider,
/// pa za njega nema zasebne tablice.
/// </summary>
public class EmailVerificationToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}

// ============================================================================
// Couple podaci (Faza 3 kraj, §4/§5): favoriti i budžetski plan vezani uz korisnika.
// Migriraju se iz localStorage nakon prijave (merge, ne pregazi).
// ============================================================================

/// <summary>
/// Favorit (par → pružatelj). Bez FK na vendors: ako pružatelj nestane (opt-out, brisanje),
/// zapis ostaje bezopasan, a čita se preko JOIN-a pa se nevažeći tiho izostave (kao frontend prune).
/// Jedinstveno po (UserId, VendorId).
/// </summary>
public class Favorite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid VendorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Budžetski plan para (jedan po korisniku — zadnji spremljeni). `caps` se NE sprema (izvodi se
/// iz total+region na klijentu/serveru), pa ovdje samo ulazi plana.
/// </summary>
public class BudgetPlan
{
    public Guid UserId { get; set; }          // PK = UserId (1:1 s korisnikom)
    public int Guests { get; set; }
    public string Region { get; set; } = "";
    public int Total { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
