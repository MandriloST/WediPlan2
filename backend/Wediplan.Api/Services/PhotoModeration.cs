using Wediplan.Api.Domain;

namespace Wediplan.Api.Services;

/// <summary>
/// Post-moderacija fotografija pružatelja (§Zadatak 15, PLAN-PRIORITETI-LANSIRANJE-3.md). Partnerova slika je JAVNA ODMAH
/// (status <c>unreviewed</c>) — admin naknadno vodi evidenciju: <c>approved</c> (pregledano, u redu) ili <c>flagged</c>
/// (neprimjereno → skriva se s javnog profila i karte, vlasnik je i dalje vidi s napomenom). NEMA pred-moderacije.
/// Čista logika (bez baze) — testabilna kao <see cref="Import.ImportMerge"/>.
/// </summary>
public static class PhotoModeration
{
    public const string Unreviewed = "unreviewed";
    public const string Approved = "approved";
    public const string Flagged = "flagged";

    public static bool IsValidStatus(string? s) => s is Unreviewed or Approved or Flagged;

    /// <summary>
    /// Dozvoljeni prijelazi: <c>unreviewed → approved|flagged</c>, <c>approved → flagged</c>, <c>flagged → approved</c>.
    /// Sve ostalo (npr. odobriti već odobreno, vratiti u unreviewed) je <c>409 invalid_transition</c>.
    /// </summary>
    public static bool CanTransition(string from, string to) => (from, to) switch
    {
        (Unreviewed, Approved) or (Unreviewed, Flagged) or (Approved, Flagged) or (Flagged, Approved) => true,
        _ => false,
    };

    /// <summary>Slika je javno vidljiva osim kad je sakrivena. MORA pratiti filter u <c>PinsController</c> (EF upit ne smije zvati ovu metodu).</summary>
    public static bool IsPublic(VendorPhoto p) => p.ModerationStatus != Flagged;

    /// <summary>
    /// Primijeni odluku admina ako je prijelaz dozvoljen: postavlja status, <c>ReviewedByUserId</c>, <c>ReviewedAt</c>.
    /// Napomena se čuva SAMO uz <c>flagged</c> (opisuje razlog skrivanja); odobravanje / vraćanje je briše.
    /// Vraća <c>false</c> (bez ikakve promjene) kad prijelaz nije dozvoljen.
    /// </summary>
    public static bool TryApply(VendorPhoto photo, string to, Guid reviewerId, string? note, DateTime now)
    {
        if (!CanTransition(photo.ModerationStatus, to)) return false;
        photo.ModerationStatus = to;
        photo.ReviewedByUserId = reviewerId;
        photo.ReviewedAt = now;
        photo.ModerationNote = to == Flagged ? note : null;
        return true;
    }
}
