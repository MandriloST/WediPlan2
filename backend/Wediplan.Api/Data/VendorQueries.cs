using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Data;

/// <summary>
/// Zajednička pravila filtriranja (Faza 2). Lista, pinovi, brojači regija i budžet MORAJU
/// koristiti ista pravila — inače broj na gumbu ne odgovara onome što par vidi nakon klika.
/// Zrcalo: frontend lib/mock/search.ts (mock način).
/// </summary>
public static class VendorQueries
{
    /// <summary>Javno vidljivi: objavljeni i bez GDPR opt-outa (§9).</summary>
    public static IQueryable<Vendor> Published(this IQueryable<Vendor> q) =>
        q.Where(v => v.IsPublished && !v.OptOut);

    /// <summary>Regija: sjedište ILI pokriva cijelu HR ILI pokriva tu regiju (§4.1).</summary>
    public static IQueryable<Vendor> InRegion(this IQueryable<Vendor> q, string? region) =>
        string.IsNullOrWhiteSpace(region)
            ? q
            : q.Where(v => v.RegionSlug == region || v.CoverageAll || v.CoverageRegions.Contains(region));

    /// <summary>Kategorija: bilo koja od svih kategorija pružatelja (§4.3).</summary>
    public static IQueryable<Vendor> InCategory(this IQueryable<Vendor> q, string? category) =>
        string.IsNullOrWhiteSpace(category)
            ? q
            : q.Where(v => v.Categories.Any(c => c.CategorySlug == category));

    /// <summary>Tekst: naziv/grad/kategorija (naziv iz šifrarnika, diacritic-insensitivno)/stil.</summary>
    public static IQueryable<Vendor> MatchText(this IQueryable<Vendor> q, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return q;
        var t = text.Trim();
        if (t.Length > 80) t = t[..80];
        // ILIKE wildcardi iz korisničkog unosa se escapeaju (\ je default escape u Postgresu)
        var like = "%" + t.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        var matchedCats = Catalog.Categories
            .Where(c => TextNorm.NormContains(c.Name, t) || (c.Short != null && TextNorm.NormContains(c.Short, t)))
            .Select(c => c.Slug)
            .ToList();

        return q.Where(v =>
            EF.Functions.ILike(v.Name, like) ||
            EF.Functions.ILike(v.City, like) ||
            matchedCats.Contains(v.CategorySlug) ||
            v.StyleTags.Any(s => EF.Functions.ILike(s, like)));
    }

    /// <summary>Stabilan redoslijed (ocjena → broj recenzija → id) — nužan za paginaciju.</summary>
    public static IOrderedQueryable<Vendor> Ranked(this IQueryable<Vendor> q) =>
        q.OrderByDescending(v => v.Rating)
         .ThenByDescending(v => v.ReviewCount)
         .ThenBy(v => v.Id);
}

/// <summary>Diacritic-insensitivna usporedba za šifrarnik u memoriji (mirror norm() iz frontenda).</summary>
public static class TextNorm
{
    public static bool NormContains(string haystack, string needle) =>
        Norm(haystack).Contains(Norm(needle));

    public static string Norm(string s)
    {
        s = s.ToLowerInvariant().Replace("đ", "d");
        var decomposed = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
