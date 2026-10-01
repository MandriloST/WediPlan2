using Wediplan.Api.Contracts;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Data;

public static class VendorMapper
{
    public static PriceDto Price(Vendor v) => Price(v.PriceKind, v.PriceFrom, v.PriceTo);

    /// <summary>Isto pravilo za projekcije (pinovi) koje nemaju cijeli entitet.</summary>
    public static PriceDto Price(string kind, int? from, int? to) => kind switch
    {
        "from" => PriceDto.FromPrice(from ?? 0),
        "perPerson" => PriceDto.PerPerson(from ?? 0, to ?? 0),
        _ => PriceDto.OnRequest(),
    };

    /// <summary>coverage kao unija: "hr" | string[] | null.</summary>
    public static object? Coverage(Vendor v) =>
        v.CoverageAll ? "hr" : (v.CoverageRegions.Count > 0 ? v.CoverageRegions.ToArray() : null);

    /// <summary>Sve kategorije, primarna prva. Vraća null ako je samo primarna (frontend fallback).</summary>
    public static IReadOnlyList<string>? Categories(Vendor v)
    {
        if (v.Categories == null || v.Categories.Count <= 1) return null;
        return v.Categories
            .OrderByDescending(c => c.IsPrimary)
            .Select(c => c.CategorySlug)
            .ToList();
    }

    public static SocialDto? Social(Vendor v) =>
        v.SocialInstagram == null && v.SocialFacebook == null
            ? null
            : new SocialDto(v.SocialInstagram, v.SocialFacebook);

    public static VendorDto ToDto(Vendor v) => new(
        Id: v.Id.ToString(),
        Slug: v.Slug,
        Name: v.Name,
        Category: v.CategorySlug,
        Region: v.RegionSlug,
        City: v.City,
        Lng: v.Lng,
        Lat: v.Lat,
        Price: Price(v),
        Rating: v.Rating,
        ReviewCount: v.ReviewCount,
        Verified: v.Verified,
        LiveCalendar: v.LiveCalendar,
        StyleTags: v.StyleTags,
        Categories: Categories(v),
        LocationPrecision: v.LocationPrecision,
        Coverage: Coverage(v),
        CoverageNote: v.CoverageNote,
        RatingSource: v.RatingSource,
        Social: Social(v),
        ClaimStatus: v.ClaimStatus == "claimed" ? "claimed" : null,
        // §Zadatak 15: sakrivene (flagged) slike se NIKAD ne vraćaju javno. Naslovna = prva javna po SortOrder,
        // pa kad je sakrivena slika bila naslovna, javno se automatski prikazuje sljedeća.
        Photos: PublicPhotoUrls(v),
        Country: v.Country == "hr" ? null : v.Country);

    /// <summary>Javni URL-ovi fotografija pružatelja po SortOrder, bez sakrivenih (§Zadatak 15); null kad nema nijedne.</summary>
    private static List<string>? PublicPhotoUrls(Vendor v)
    {
        if (v.Photos == null) return null;
        var urls = v.Photos.Where(Wediplan.Api.Services.PhotoModeration.IsPublic)
            .OrderBy(p => p.SortOrder).Select(p => p.StorageKey).ToList();
        return urls.Count > 0 ? urls : null;
    }
}
