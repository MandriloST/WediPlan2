using Wediplan.Api.Domain;

namespace Wediplan.Api.Import;

/// <summary>
/// Spajanje retka iz Excela (<c>incoming</c>) u postojećeg pružatelja iz baze (<c>existing</c>) pri
/// ponovnom importu (§Zadatak 10, PLAN-PRIORITETI-LANSIRANJE-3.md).
/// <para>
/// Izdvojeno iz <see cref="ExcelImporter"/> kao čista funkcija (bez baze i bez Excela) da bude
/// testabilna. Problem koji rješava: prije je import prepisivao SVA polja, pa bi se izmjene koje je
/// partner uredio i objavio na preuzetom profilu tiho vratile na vrijednosti iz Excela.
/// </para>
/// </summary>
public static class ImportMerge
{
    /// <summary>
    /// Polja kojima upravlja PARTNER (uređuje ih kroz draft i objavljuje). MORA pratiti
    /// <c>ProviderMapper.ApplyToVendor</c>: About, Services, PriceKind/PriceFrom/PriceTo, StyleTags.
    /// Cijena se u izvještaju navodi jednom, kao "Price" (tri stupca = jedna poslovna informacija).
    /// Ako se u <c>ApplyToVendor</c> doda novo polje, dodati ga i ovdje i u <see cref="Apply"/>.
    /// </summary>
    public static readonly string[] PartnerManagedFields = { "About", "Services", "Price", "StyleTags" };

    /// <summary>
    /// Primijeni <paramref name="incoming"/> na <paramref name="existing"/>.
    /// <list type="bullet">
    /// <item>Polja kojima upravlja Wediplan (naziv, lokacija, pokrivanje, ocjena, bedževi, kontakti) kopiraju se uvijek.</item>
    /// <item>Partnerska polja kopiraju se samo ako profil NIJE preuzet (<c>ClaimStatus != "claimed"</c>).
    /// Zaštita ovisi o <c>ClaimStatus</c>, ne o <c>OwnerUserId</c>: kad vlasnik obriše račun,
    /// <c>OwnerUserId</c> postaje null, ali <c>ClaimStatus</c> ostaje <c>claimed</c> i izmjene ostaju zaštićene.</item>
    /// </list>
    /// Vraća popis partnerskih polja koja NISU prepisana jer se Excel razlikuje od baze (prazan popis =
    /// nema se što prijaviti). Ne dira <c>ClaimStatus</c>, <c>OwnerUserId</c>, <c>OptOut</c>,
    /// <c>IsPublished</c>, kategorije ni recenzije.
    /// </summary>
    public static IReadOnlyList<string> Apply(Vendor existing, Vendor incoming)
    {
        // --- polja kojima upravlja Wediplan: uvijek ---
        existing.Name = incoming.Name;
        existing.CategorySlug = incoming.CategorySlug;
        existing.RegionSlug = incoming.RegionSlug;
        existing.Country = incoming.Country;
        existing.City = incoming.City;
        existing.Lat = incoming.Lat;
        existing.Lng = incoming.Lng;
        existing.LocationPrecision = incoming.LocationPrecision;
        existing.CoverageAll = incoming.CoverageAll;
        existing.CoverageRegions = incoming.CoverageRegions;
        existing.CoverageNote = incoming.CoverageNote;
        existing.Rating = incoming.Rating;
        existing.ReviewCount = incoming.ReviewCount;
        existing.RatingSource = incoming.RatingSource;
        existing.Verified = incoming.Verified;
        existing.LiveCalendar = incoming.LiveCalendar;
        existing.Website = incoming.Website;
        existing.Phone = incoming.Phone;
        existing.Email = incoming.Email;
        existing.SocialInstagram = incoming.SocialInstagram;
        existing.SocialFacebook = incoming.SocialFacebook;

        // --- partnerska polja ---
        var skipped = new List<string>();
        if (existing.ClaimStatus == "claimed")
        {
            if (!string.Equals(existing.About, incoming.About, StringComparison.Ordinal)) skipped.Add("About");
            if (!SameList(existing.Services, incoming.Services)) skipped.Add("Services");
            if (existing.PriceKind != incoming.PriceKind
                || existing.PriceFrom != incoming.PriceFrom
                || existing.PriceTo != incoming.PriceTo) skipped.Add("Price");
            if (!SameList(existing.StyleTags, incoming.StyleTags)) skipped.Add("StyleTags");
        }
        else
        {
            existing.About = incoming.About;
            existing.Services = incoming.Services;
            existing.PriceKind = incoming.PriceKind;
            existing.PriceFrom = incoming.PriceFrom;
            existing.PriceTo = incoming.PriceTo;
            existing.StyleTags = incoming.StyleTags;
        }

        existing.UpdatedAt = DateTime.UtcNow;
        return skipped;
    }

    /// <summary>Usporedba lista po sadržaju i redoslijedu; null se tretira kao prazna lista.</summary>
    private static bool SameList(List<string>? a, List<string>? b) =>
        (a ?? new List<string>()).SequenceEqual(b ?? new List<string>());
}
