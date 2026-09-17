using Wediplan.Api.Contracts;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Data;

/// <summary>
/// Pretvorbe za Fazu 4: draft ↔ DTO, seed drafta iz žive verzije, primjena drafta na pružatelja
/// (objava). Dijele ih ProviderController (spremanje/objava vlasnika) i AdminController (odobrenje).
/// </summary>
public static class ProviderMapper
{
    private static readonly string[] AllowedPriceKinds = { "from", "perPerson", "onRequest" };

    public static VendorDraftDto ToDto(VendorDraft d) => new(
        About: d.About,
        Services: d.Services,
        Price: VendorMapper.Price(d.PriceKind, d.PriceFrom, d.PriceTo),
        StyleTags: d.StyleTags);

    /// <summary>Prvi put kad pružatelj otvori uređivanje: draft se popuni iz žive verzije.</summary>
    public static VendorDraft SeedFromVendor(Vendor v) => new()
    {
        VendorId = v.Id,
        About = v.About,
        Services = v.Services?.ToList() ?? new(),
        PriceKind = v.PriceKind,
        PriceFrom = v.PriceFrom,
        PriceTo = v.PriceTo,
        StyleTags = v.StyleTags?.ToList() ?? new(),
    };

    /// <summary>
    /// Upiše dolazni DTO u draft (uz validaciju/čišćenje). Vraća poruku greške ili null.
    /// Ne dira Rating/ReviewCount/Verified — to nisu polja koja pružatelj uređuje.
    /// </summary>
    public static string? ApplyDto(VendorDraft d, VendorDraftDto dto)
    {
        var kind = dto.Price?.Kind ?? "onRequest";
        if (!AllowedPriceKinds.Contains(kind)) return "invalid_price_kind";

        int? from = dto.Price?.From;
        int? to = dto.Price?.To;
        if (kind == "from")
        {
            if (from is null or < 0) return "invalid_price";
            to = null;
        }
        else if (kind == "perPerson")
        {
            if (from is null or < 0 || to is null or < 0 || to < from) return "invalid_price";
        }
        else // onRequest
        {
            from = null; to = null;
        }

        d.About = string.IsNullOrWhiteSpace(dto.About) ? null : dto.About.Trim();
        d.Services = (dto.Services ?? new List<string>())
            .Select(s => s.Trim()).Where(s => s.Length > 0).Take(20).ToList();
        d.StyleTags = (dto.StyleTags ?? new List<string>())
            .Select(s => s.Trim()).Where(s => s.Length > 0).Take(12).ToList();
        d.PriceKind = kind; d.PriceFrom = from; d.PriceTo = to;
        d.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    /// <summary>Objava: kopira draft u živu verziju pružatelja (§6.3/§6.4).</summary>
    public static void ApplyToVendor(VendorDraft d, Vendor v)
    {
        v.About = d.About;
        v.Services = d.Services.ToList();
        v.PriceKind = d.PriceKind;
        v.PriceFrom = d.PriceFrom;
        v.PriceTo = d.PriceTo;
        v.StyleTags = d.StyleTags.ToList();
        v.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Thumbnail URL po konvenciji: "{base}.webp" → "{base}_thumb.webp".</summary>
    public static string ThumbUrl(string url) =>
        url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? url[..^5] + "_thumb.webp" : url;

    public static ProviderPhotoDto PhotoDto(VendorPhoto p) =>
        new(p.Id.ToString(), p.StorageKey, ThumbUrl(p.StorageKey), p.IsCover, p.SortOrder);

    /// <summary>Domena iz e-maila (dio iza @, lowercase) ili null.</summary>
    public static string? EmailDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.LastIndexOf('@');
        return at >= 0 && at < email.Length - 1 ? email[(at + 1)..].Trim().ToLowerInvariant() : null;
    }

    /// <summary>Registrabilna domena iz URL-a web-stranice (host bez "www."), ili null.</summary>
    public static string? WebsiteDomain(string? website)
    {
        if (string.IsNullOrWhiteSpace(website)) return null;
        var s = website.Trim();
        if (!s.Contains("://")) s = "https://" + s;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri)) return null;
        var host = uri.Host.ToLowerInvariant();
        return host.StartsWith("www.") ? host[4..] : host;
    }
}
