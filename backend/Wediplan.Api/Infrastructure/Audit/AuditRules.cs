using System.Collections;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Infrastructure.Audit;

/// <summary>
/// Pravila dnevnika promjena (Zadatak 14): koje entitete pratimo, koja svojstva ignoriramo, a koja bilježimo
/// BEZ vrijednosti (minimizacija osobnih podataka, GDPR). Čista statička logika — bez EF-a, pa je izravno testabilna.
/// </summary>
public static class AuditRules
{
    /// <summary>CLR tip → <c>audit_log.entity_type</c>. Sve što nije ovdje se NE prati (VendorDraft, Favorite,
    /// BudgetPlan, Event, DailyStat, tokeni, Identity tablice i sam AuditLog — nema rekurzije).</summary>
    private static readonly Dictionary<Type, string> Tracked = new()
    {
        [typeof(Vendor)] = "vendor",
        [typeof(VendorPhoto)] = "vendor_photo",
        [typeof(ImportedReview)] = "imported_review",
        [typeof(UserReview)] = "user_review",
        [typeof(Claim)] = "claim",
    };

    /// <summary>Svojstva koja nikad ne ulaze u dnevnik: <c>UpdatedAt</c> (šum — mijenja se uz svaku promjenu)
    /// i <c>Search</c> (generirani tsvector). Navigacije se ne pojavljuju među svojstvima unosa.</summary>
    private static readonly HashSet<string> Ignored = new() { "UpdatedAt", "Search" };

    /// <summary>Bilježi se samo da je polje promijenjeno/postavljeno, bez starog i novog sadržaja:
    /// kontakti pružatelja (osobni podaci obrtnika), bilješka o privoli, te slobodan tekst pojedinaca
    /// (recenzije, poruka uz zahtjev za preuzimanje). Povijest njihovog teksta ne treba nam za GDPR svrhu.</summary>
    private static readonly HashSet<string> Masked = new()
    {
        "vendor.Phone", "vendor.Email", "vendor.SocialInstagram", "vendor.SocialFacebook", "vendor.ConsentNote",
        "user_review.Text",
        "imported_review.Author", "imported_review.Text",
        "claim.Message",
    };

    /// <summary>Pri brisanju bilježimo samo ono što pomaže prepoznati što je obrisano (nikad osobne podatke).</summary>
    private static readonly Dictionary<string, string[]> DeleteIdentifiers = new()
    {
        ["vendor_photo"] = new[] { "StorageKey" },
    };

    public static string? EntityTypeOf(Type clrType) => Tracked.TryGetValue(clrType, out var t) ? t : null;

    public static bool IsIgnored(string entityType, string property) => Ignored.Contains(property);

    public static bool IsMasked(string entityType, string property) => Masked.Contains($"{entityType}.{property}");

    public static bool KeepOnDelete(string entityType, string property) =>
        DeleteIdentifiers.TryGetValue(entityType, out var keep) && keep.Contains(property);

    /// <summary>
    /// Jednakost po SADRŽAJU: liste (<c>Services</c>, <c>StyleTags</c>, <c>CoverageRegions</c>, <c>ConsentScope</c>)
    /// se uspoređuju element po element, ne po referenci — inače bi zamjena liste istim sadržajem
    /// (npr. <c>v.Services = d.Services</c>) izgledala kao promjena.
    /// </summary>
    public static bool ValuesEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a is string || b is string) return a.Equals(b);
        if (a is IEnumerable ea && b is IEnumerable eb)
        {
            var la = ea.Cast<object?>().ToList();
            var lb = eb.Cast<object?>().ToList();
            if (la.Count != lb.Count) return false;
            for (var i = 0; i < la.Count; i++)
                if (!Equals(la[i], lb[i])) return false;
            return true;
        }
        return a.Equals(b);
    }
}
