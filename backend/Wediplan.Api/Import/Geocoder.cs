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

    /// <summary>
    /// Vraća [lat,lng] ili null (nije nađeno). Kešira i pozitivne i negativne rezultate.
    /// countryCode: "hr" (default) | "ba" | "si" — inozemni gradovi (BiH fotografi) se
    /// geokodiraju u svojoj državi; ključ cachea za "hr" ostaje kompatibilan sa starim.
    /// </summary>
    public async Task<double[]?> GeocodeAsync(string city, string regionName, CancellationToken ct, string countryCode = "hr")
    {
        var key = countryCode == "hr"
            ? ImportRules.Norm($"{city}|{regionName}")
            : ImportRules.Norm($"{city}|{countryCode}");
        if (_cache.TryGetValue(key, out var cached)) { Hits++; return cached; }

        // rate limit 1 req/s
        var wait = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - _lastCall);
        if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
        _lastCall = DateTime.UtcNow;

        double[]? result = null;
        try
        {
            var place = countryCode == "hr"
                ? $"{city}, {regionName}, Hrvatska"
                : $"{city}, {ImportRules.CountryName(countryCode)}";
            var q = Uri.EscapeDataString(place);
            var url = $"https://nominatim.openstreetmap.org/search?q={q}&format=json&limit=1&countrycodes={countryCode}";
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.GetArrayLength() > 0)
            {
                var first = doc.RootElement[0];
                var lat = double.Parse(first.GetProperty("lat").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                var lng = double.Parse(first.GetProperty("lon").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                if (lat is >= 42 and <= 47 && lng is >= 13 and <= 20) result = new[] { lat, lng };
            }
        }
        catch { Failures++; }

        Misses++;
        _cache[key] = result;
        return result;
    }

    public void Save() =>
        File.WriteAllText(_cachePath, JsonSerializer.Serialize(_cache,
            new JsonSerializerOptions { WriteIndented = false }));
}
