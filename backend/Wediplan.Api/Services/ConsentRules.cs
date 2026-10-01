using Wediplan.Api.Domain;

namespace Wediplan.Api.Services;

/// <summary>
/// Privola pružatelja koja proizlazi iz sustava (§Zadatak 17). Čista logika — testabilna bez baze.
/// </summary>
public static class ConsentRules
{
    /// <summary>Za što vrijedi privola dana preuzimanjem profila: podaci, slike i recenzije.</summary>
    public static readonly string[] ClaimScope = { "data", "photos", "reviews" };

    /// <summary>
    /// Odobren claim = pružatelj je sam preuzeo profil i prihvatio uvjete za partnere, što je privola: status <c>granted</c>,
    /// kanal <c>claim</c>, trenutak <paramref name="now"/>, opseg <see cref="ClaimScope"/>.
    /// <b>Izuzetak:</b> ako je privola <c>refused</c>, ne mijenja se (vraća <c>false</c>) — odbijanje se ne poništava tiho.
    /// <para><b>OPS pretpostavka:</b> da bi ovo bilo pravno utemeljeno, tekst uvjeta za partnere mora izričito navesti da preuzimanjem
    /// profila pružatelj pristaje na objavu podataka, fotografija i recenzija.</para>
    /// </summary>
    public static bool GrantViaClaim(Vendor vendor, DateTime now)
    {
        if (vendor.ConsentStatus == "refused") return false;
        vendor.ConsentStatus = "granted";
        vendor.ConsentChannel = "claim";
        vendor.ConsentAt = now;
        vendor.ConsentScope = new List<string>(ClaimScope);
        return true;
    }
}
