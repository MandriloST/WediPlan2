using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Wediplan.Api.Import;

/// <summary>
/// Čiste funkcije parsiranja/validacije — VJERAN PORT scripts/import-vendors.mjs.
/// Ako se promijeni Node skripta ili lib/data.ts, uskladiti i ovdje.
/// </summary>
public static class ImportRules
{
    public static string Norm(string? s)
    {
        s = (s ?? "").ToLowerInvariant().Replace("đ", "d");
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        s = sb.ToString().Normalize(NormalizationForm.FormC);
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    public static string Slugify(string? s) =>
        Regex.Replace(Regex.Replace(Norm(s), @"[^a-z0-9]+", "-"), @"^-|-$", "");

    public static bool YesNo(string? v) => Norm(v) == "da";

    // Šifrarnici (nazivi → slug) — moraju pratiti Catalog.cs / lib/data.ts.
    private static readonly Dictionary<string, string> Regions = new()
    {
        ["istra"] = "Istra", ["kvarner"] = "Kvarner", ["dalmacija"] = "Dalmacija",
        ["zagreb"] = "Zagreb i okolica", ["slavonija"] = "Slavonija",
    };

    public static readonly Dictionary<string, string> CatLookup = BuildCatLookup();
    public static readonly Dictionary<string, string> RegLookup = BuildRegLookup();

    // Spojene/ukinute kategorije → nasljednica.
    public static readonly Dictionary<string, string> Merged = new()
    {
        [Norm("Auto za mladence (rent a car)")] = "najam-limuzina",
        [Norm("Auto za mladence")] = "najam-limuzina",
    };

    // Kategorije kojima je fizička lokacija bit ponude.
    public static readonly HashSet<string> VenueCategories = new()
    { "restorani-i-sale", "konobe-i-prostori", "najam-kuce" };

    private static Dictionary<string, string> BuildCatLookup()
    {
        var m = new Dictionary<string, string>();
        foreach (var c in Data.Catalog.Categories)
        {
            m[Norm(c.Name)] = c.Slug;
            m[c.Slug] = c.Slug;
        }
        m[Norm("Glazba - bendovi")] = "glazba-bendovi";
        m[Norm("Glazba bendovi")] = "glazba-bendovi";
        m[Norm("Čuvanje i animacije djece")] = "cuvanje-djece";
        return m;
    }

    private static Dictionary<string, string> BuildRegLookup()
    {
        var m = new Dictionary<string, string>();
        foreach (var (id, name) in Regions)
        {
            m[Norm(name)] = id;
            m[id] = id;
        }
        return m;
    }

    /// <summary>IG/FB: "@handle" | "handle" | URL → kanonski https URL, ili null.</summary>
    public static string? SocialUrl(string? raw, string kind)
    {
        var s = (raw ?? "").Trim();
        if (s.Length == 0) return null;
        s = Regex.Replace(s, @"^@", "");
        s = Regex.Replace(s, @"^https?://", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"^www\.", "", RegexOptions.IgnoreCase);
        var host = kind == "instagram" ? "instagram.com" : "facebook.com";
        var m = Regex.Match(s, @"(?:instagram\.com|facebook\.com|fb\.com|fb\.me)/(.+)", RegexOptions.IgnoreCase);
        var path = (m.Success ? m.Groups[1].Value : s).TrimStart('/').TrimEnd('/').Split('?', '#')[0];
        return path.Length == 0 ? null : $"https://{host}/{path}";
    }

    public record Coords(double Lat, double Lng, bool OutOfCroatia = false);

    public static Coords? ParseCoords(string? raw)
    {
        var s = (raw ?? "").Replace(";", ",");
        var m = Regex.Match(s, @"(-?\d+[.,]?\d*)\s*,\s*(-?\d+[.,]?\d*)");
        if (!m.Success) return null;
        double lat = double.Parse(m.Groups[1].Value.Replace(",", "."), CultureInfo.InvariantCulture);
        double lng = double.Parse(m.Groups[2].Value.Replace(",", "."), CultureInfo.InvariantCulture);
        if (lat is >= 13 and <= 20 && lng is >= 42 and <= 47) (lat, lng) = (lng, lat); // auto-swap
        if (lat < 42 || lat > 47 || lng < 13 || lng > 20) return new Coords(lat, lng, true);
        return new Coords(lat, lng);
    }

    /// <summary>"cijela hrvatska"→"hr" | "Dalmacija;Kvarner"→ids. Nepoznata regija → unknown lista.</summary>
    public static (bool All, List<string> Ids, List<string> Unknown) ParseCoverage(string? raw)
    {
        var s = (raw ?? "").Trim();
        var ids = new List<string>();
        var unknown = new List<string>();
        if (s.Length == 0) return (false, ids, unknown);
        if (new[] { "cijela hrvatska", "hrvatska", "hr", "sve" }.Contains(Norm(s)))
            return (true, ids, unknown);
        foreach (var partRaw in Regex.Split(s, @"[;,]"))
        {
            var part = partRaw.Trim();
            if (part.Length == 0) continue;
            if (RegLookup.TryGetValue(Norm(part), out var id))
            {
                if (!ids.Contains(id)) ids.Add(id);
            }
            else unknown.Add(part);
        }
        return (false, ids, unknown);
    }

    public static string? ResolveCategory(string? raw)
    {
        var n = Norm(raw);
        if (CatLookup.TryGetValue(n, out var c)) return c;
        if (Merged.TryGetValue(n, out var m)) return m;
        return null;
    }

    public static string? ResolveRegion(string? raw) =>
        RegLookup.TryGetValue(Norm(raw), out var r) ? r : null;

    /* --------------------- Inozemni pružatelji (BiH/SI) --------------------- */

    /// <summary>Poznati BiH gradovi za auto-detekciju (retci bez HR regije).</summary>
    private static readonly HashSet<string> BihCities = new(new[]
    {
        "sarajevo","banja luka","mostar","bihac","kljuc","novi travnik","ljubuski",
        "bijeljina","zepce","posusje","tesanj","kiseljak","brcko","tuzla","busovaca",
        "prijedor","siroki brijeg","capljina","citluk","medugorje","grude","livno",
        "tomislavgrad","zenica","travnik","vitez","orasje","neum","trebinje","stolac",
        "konjic","jajce","doboj","gradiska","derventa","modrica","zvornik","visoko",
        "kakanj","bugojno","gornji vakuf","prozor","srebrenik","gracanica","lukavac",
        "cazin","velika kladusa","sanski most","odzak","zavidovici",
    }.Select(Norm));

    /// <summary>Očisti grad: "Banja Luka (BiH)" → "Banja Luka", "Bijeljina / destination" → "Bijeljina".</summary>
    public static string CleanCity(string? raw) =>
        (raw ?? "").Split('/', '(', ';', '–', '-')[0]
            .Replace(" i okolica", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

    /// <summary>
    /// Interna regija (naziv ili slug) → SLUŽBENA hrvatska županija koju OSM/Nominatim poznaje.
    /// Naše regije ("Dalmacija", "Zagreb i okolica", "Kvarner") nisu administrativne jedinice, pa
    /// se za geokodiranje moraju preslikati. Za regije koje pokrivaju više županija biramo onu s
    /// glavnim gradom regije (Split, Rijeka, Zagreb…) — dovoljno za centroid grada; točnu poziciju
    /// ionako daje sam grad u upitu, a raspored pinova radi frontend jitter. null = ne dodaji
    /// županiju (npr. nepoznata regija) → fallback na "grad, Hrvatska".
    /// </summary>
    public static string? CountyForRegion(string? region)
    {
        switch (Norm(region))
        {
            case "istra": return "Istarska županija";
            case "kvarner": return "Primorsko-goranska županija";
            case "dalmacija": return "Splitsko-dalmatinska županija";
            case "zagreb":
            case "zagreb i okolica": return "Grad Zagreb";
            case "slavonija": return "Osječko-baranjska županija";
            default: return null;
        }
    }

    /// <summary>
    /// Odredi državu iz eksplicitnog stupca `drzava` (ako postoji u Excelu) ili
    /// auto-detekcijom BiH grada. Vraća "hr" | "ba" | "si" | null (nepoznato).
    /// </summary>
    public static string? DetectCountry(string? drzavaRaw, string? cityRaw)
    {
        var d = Norm(drzavaRaw);
        if (d.Length > 0)
        {
            if (d is "hr" or "hrvatska" or "croatia") return "hr";
            if (d is "ba" or "bih" or "bosna" or "bosna i hercegovina") return "ba";
            if (d is "si" or "slovenija" or "slovenia") return "si";
            return null; // nepoznata vrijednost — importer upozorava
        }
        var city = Norm(CleanCity(cityRaw));
        if (city is "bih" or "bosna i hercegovina") return "ba";
        if (BihCities.Contains(city)) return "ba";
        return null;
    }

    public static string CountryName(string code) => code switch
    {
        "ba" => "Bosna i Hercegovina",
        "si" => "Slovenija",
        _ => "Hrvatska",
    };
}
