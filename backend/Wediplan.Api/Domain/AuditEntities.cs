namespace Wediplan.Api.Domain;

/// <summary>
/// Append-only dnevnik promjena (GDPR; Zadatak 13 — tablica, Zadatak 14 — automatsko punjenje).
/// Bilježi TKO je, KADA i ŠTO promijenio na partnerskim/javnim podacima. NAMJERNO bez FK-ova:
/// zapisi moraju preživjeti brisanje korisnika i entiteta na koje se odnose.
/// <para>Minimizacija podataka: kontakt polja (telefon, e-mail, IG, FB) se bilježe bez vrijednosti
/// (<c>{"changed":true}</c>). Rok čuvanja 24 mjeseca (CLI <c>--audit-prune</c>, Zadatak 14).</para>
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>admin | partner | user | public | import | system.</summary>
    public string ActorType { get; set; } = "system";
    /// <summary>Bez FK — pseudonimni GUID koji nakon brisanja korisnika više ne pokazuje ni na koga.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>vendor | vendor_photo | imported_review | user_review | claim | user.</summary>
    public string EntityType { get; set; } = "";
    /// <summary>Id entiteta kao string (Guid).</summary>
    public string EntityId { get; set; } = "";
    /// <summary>create | update | delete | optout | optout_restored | account_deleted | owner_unlinked | …</summary>
    public string Action { get; set; } = "";

    /// <summary>jsonb: {"polje":{"old":…,"new":…}} ili {"polje":{"changed":true}} za maskirana polja.</summary>
    public string? Changes { get; set; }
    /// <summary>npr. "api:PUT /api/provider/…" ili "import:vendors-2026-09.xlsx".</summary>
    public string? Source { get; set; }
    public string? Note { get; set; }
}
