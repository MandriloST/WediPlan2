using System.Text.Json;

namespace Wediplan.Api.Import;

/// <summary>
/// Geokodira "grad, regija, Hrvatska" → (lat,lng) preko Nominatima (OpenStreetMap).
/// - Poštuje Nominatim politiku: max 1 zahtjev/s, obavezan User-Agent.
/// - Trajni cache (geocode-cache.json) — ponovni importi ne zovu mrežu.
/// - precision ostaje "city" (centroid grada); frontend radi jitter (Zadatak D).
/// </summary>
public class Geocoder
{
    private readonly string _cachePath;
    private readonly Dictionary<string, double[]?> _cache;
    private readonly HttpClient _http;
    private DateTime _lastCall = DateTime.MinValue;

    public int Hits { get; private set; }
    public int Misses { get; private set; }
    public int Failures { get; private set; }

    public Geocoder(string cachePath)
    {
        _cachePath = cachePath;
        _cache = File.Exists(cachePath)
            ? JsonSerializer.Deserialize<Dictionary<string, double[]?>>(File.ReadAllText(cachePath)) ?? new()
            : new();
        _http = new HttpClient();
        // Nominatim ZAHTIJEVA prepoznatljiv User-Agent s kontaktom (zamijenite email).
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("wediplan-import/1.0 (kontakt@wediplan.hr)");
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    /// <summary>Ako je true, ključevi s null vrijednošću iz starijeg importa se PONOVNO pokušavaju
    /// (npr. nakon popravka upita). Postavlja se iz CLI zastavice --geocode-retry.</summary>
    public bool RetryNegatives { get; init; }

    /// <summary>Grubi granični okviri po državi (lat/lng) — filtriraju očito krive pogotke.</summary>
    private static bool InCountryBox(string cc, double lat, double lng) => cc switch
    {
        "hr" => lat is >= 42 and <= 47 && lng is >= 13 and <= 20,
        "ba" => lat is >= 42 and <= 45.5 && lng is >= 15.5 and <= 20,
        "si" => lat is >= 45 and <= 47 && lng is >= 13 and <= 16.7,
        _ => lat is >= 35 and <= 72 && lng is >= -25 and <= 45, // Europa (široko)
    };

    /// <summary>
    /// Vraća [lat,lng] ili null (nije nađeno). Kešira i pozitivne i negativne rezultate.
    /// countryCode: "hr" (default) | "ba" | "si".
    ///
    /// Ključni popravak (2026-09-16): raniji upit je koristio izmišljene "regije"
    /// ("Dalmacija", "Zagreb i okolica", "Kvarner"), koje OSM ne poznaje → Nominatim je za
    /// Split/Zagreb/Rijeku i sve gradove tih regija vraćao prazno i to trajno keširao kao null.
    /// Sada: (1) `regionName` se preslikava u SLUŽBENU županiju (Split → Splitsko-dalmatinska
    /// županija), (2) ako to ne uspije, pokušava se samo "grad, Hrvatska" i strukturirani
    /// `city=` upit, (3) negativni cache se može poništiti s RetryNegatives.
    /// `city` mora biti već očišćen (ImportRules.CleanCity — bez "/", "(", … ).
    /// </summary>
    public async Task<double[]?> GeocodeAsync(string city, string regionName, CancellationToken ct, string countryCode = "hr")
    {
        var key = countryCode == "hr"
            ? ImportRules.Norm($"{city}|{regionName}")
            : ImportRules.Norm($"{city}|{countryCode}");
        if (_cache.TryGetValue(key, out var cached) && !(cached == null && RetryNegatives))
        {
            Hits++;
            return cached;
        }

        // Kandidatski upiti, od najpreciznijeg prema širem. Prvi pogodak unutar okvira pobjeđuje.
        var county = countryCode == "hr" ? ImportRules.CountyForRegion(regionName) : null;
        var candidates = new List<(string q, bool structured)>();
        if (county != null) candidates.Add(($"{city}, {county}, Hrvatska", false));
        if (countryCode == "hr")
        {
            candidates.Add(($"{city}, Hrvatska", false));
            candidates.Add((city, true)); // strukturirani city= upit (v. dolje)
        }
        else
        {
            candidates.Add(($"{city}, {ImportRules.CountryName(countryCode)}", false));
            candidates.Add((city, true));
        }

        double[]? result = null;
        foreach (var (q, structured) in candidates)
        {
            result = await QueryAsync(q, countryCode, structured, ct);
            if (result != null) break;
        }

        Misses++;
        _cache[key] = result;
        return result;
    }

    private async Task<double[]?> QueryAsync(string place, string cc, bool structured, CancellationToken ct)
    {
        // rate limit 1 req/s (Nominatim politika)
        var wait = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - _lastCall);
        if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
        _lastCall = DateTime.UtcNow;

        try
        {
            var url = structured
                ? $"https://nominatim.openstreetmap.org/search?city={Uri.EscapeDataString(place)}&format=json&limit=1&countrycodes={cc}"
                : $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(place)}&format=json&limit=1&countrycodes={cc}";
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.GetArrayLength() > 0)
            {
                var first = doc.RootElement[0];
                var lat = double.Parse(first.GetProperty("lat").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                var lng = double.Parse(first.GetProperty("lon").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                if (InCountryBox(cc, lat, lng)) return new[] { lat, lng };
            }
        }
        catch { Failures++; }
        return null;
    }

    public void Save() =>
        File.WriteAllText(_cachePath, JsonSerializer.Serialize(_cache,
            new JsonSerializerOptions { WriteIndented = false }));
}
