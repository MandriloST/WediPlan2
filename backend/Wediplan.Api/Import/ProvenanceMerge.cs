using System.Globalization;
using System.Text.RegularExpressions;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Import;

/// <summary>
/// Porijeklo podataka i privola pružatelja iz jednog retka Excela (§Zadatak 17). <b>null = prazna ćelija</b> (ili neispravna
/// vrijednost koja je ignorirana) → import NE mijenja postojeću vrijednost u bazi. Vrijednosti su već mapirane na interne
/// (<c>dano → granted</c> …). Admin-interno — nikad u javni API.
/// </summary>
public sealed record ProvenanceInput(
    string? DataSource,
    DateTime? DataCollectedAt,
    string? ConsentStatus,
    DateTime? ConsentRequestedAt,
    DateTime? ConsentAt,
    string? ConsentChannel,
    List<string>? ConsentScope,
    string? ConsentNote,
    string? GooglePlaceId);

/// <summary>
/// Parsiranje i mapiranje stupaca porijekla/privole (HR → interne vrijednosti) — čista logika bez Excela i baze,
/// pa je testabilna. Vrijednosti MORAJU pratiti <c>scripts/make-template.py</c> (padajuće liste) i <c>lib/provenance.ts</c>.
/// </summary>
public static class ProvenanceRules
{
    public static readonly string[] DataSources = { "google_maps", "web", "instagram", "facebook", "partner", "preporuka", "drugo" };
    public static readonly string[] ConsentStatuses = { "unknown", "requested", "granted", "refused" };
    /// <summary>Kanali koje smije upisati Excel. <c>claim</c> postavlja isključivo sustav pri odobrenju claima (v. <c>ConsentRules</c>).</summary>
    public static readonly string[] ConsentChannelsFromExcel = { "email", "instagram", "facebook", "telefon", "osobno" };
    /// <summary>Svi kanali (uključujući <c>claim</c>) — dozvoljeni pri ručnoj korekciji u adminu.</summary>
    public static readonly string[] ConsentChannelsAll = { "email", "instagram", "facebook", "telefon", "osobno", "claim" };
    public static readonly string[] ConsentScopes = { "data", "photos", "reviews" };

    private static readonly Dictionary<string, string> SourceAlias = new()
    {
        ["google_maps"] = "google_maps", ["google"] = "google_maps", ["googlemaps"] = "google_maps", ["maps"] = "google_maps",
        ["web"] = "web", ["instagram"] = "instagram", ["ig"] = "instagram", ["facebook"] = "facebook", ["fb"] = "facebook",
        ["partner"] = "partner", ["preporuka"] = "preporuka", ["drugo"] = "drugo",
    };

    // Norm() uklanja dijakritike ("zatraženo" → "zatrazeno"), pa su ključevi bez njih.
    private static readonly Dictionary<string, string> StatusAlias = new()
    {
        ["nepoznato"] = "unknown", ["unknown"] = "unknown",
        ["zatrazeno"] = "requested", ["zatrazena"] = "requested", ["requested"] = "requested",
        ["dano"] = "granted", ["dana"] = "granted", ["granted"] = "granted",
        ["odbijeno"] = "refused", ["odbijena"] = "refused", ["refused"] = "refused",
    };

    private static readonly Dictionary<string, string> ChannelAlias = new()
    {
        ["email"] = "email", ["e_mail"] = "email", ["mail"] = "email",
        ["instagram"] = "instagram", ["ig"] = "instagram", ["facebook"] = "facebook", ["fb"] = "facebook",
        ["telefon"] = "telefon", ["phone"] = "telefon", ["poziv"] = "telefon",
        ["osobno"] = "osobno", ["uzivo"] = "osobno",
    };

    private static readonly Dictionary<string, string> ScopeAlias = new()
    {
        ["podaci"] = "data", ["data"] = "data",
        ["slike"] = "photos", ["fotografije"] = "photos", ["photos"] = "photos",
        ["recenzije"] = "reviews", ["reviews"] = "reviews",
    };

    private static string Key(string? s) => ImportRules.Norm(s).Replace(' ', '_').Replace('-', '_');

    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ",
        "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yyyy HH:mm:ss", "d.M.yyyy H:mm:ss", "dd/MM/yyyy", "d/M/yyyy",
    };

    /// <summary>
    /// Datum iz ćelije: ISO (<c>2026-09-30</c>), hrvatski (<c>30.09.2026.</c>, <c>30. 9. 2026.</c>), <c>dd/MM/yyyy</c> ili Excel serijski broj.
    /// Vraća ponoć UTC (<c>DateTimeKind.Utc</c> — Npgsql za <c>timestamptz</c> odbija drugi Kind). Prazno ili neispravno (ili godina izvan 2000–2100) → <c>null</c>.
    /// </summary>
    public static DateTime? ParseDate(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (s.Length == 0) return null;

        var candidates = new[] { s.TrimEnd('.'), Regex.Replace(s.TrimEnd('.'), @"\.\s+", ".") };
        foreach (var c in candidates)
            if (DateTime.TryParseExact(c, DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d))
                return InRange(d.Date);

        // Excel serijski broj (npr. 46295) — samo razumni raspon, da "5" ne postane 1900.
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial >= 36526 && serial < 73051)
        {
            try { return InRange(DateTime.FromOADate(serial).Date); } catch (ArgumentException) { return null; }
        }
        return null;
    }

    /// <summary>
    /// Npgsql za <c>timestamptz</c> odbija <c>DateTimeKind.Unspecified</c>, a JSON datum bez zone (<c>"2026-09-30T00:00:00"</c>) tako stiže.
    /// Utc ostaje; Local se pretvara; Unspecified se tumači kao UTC.
    /// </summary>
    public static DateTime? AsUtc(DateTime? d)
    {
        if (d == null) return null;
        var v = d.Value;
        return v.Kind switch
        {
            DateTimeKind.Utc => v,
            DateTimeKind.Local => v.ToUniversalTime(),
            _ => DateTime.SpecifyKind(v, DateTimeKind.Utc),
        };
    }

    private static DateTime? InRange(DateTime d) =>
        d.Year is >= 2000 and <= 2100 ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null;

    /// <summary>
    /// Pročitaj stupce porijekla i privole iz retka (<paramref name="col"/>: naziv stupca → sirova vrijednost). Neispravne vrijednosti
    /// se ignoriraju UZ upozorenje u <paramref name="warnings"/> (poruke bez broja retka i naziva — njih dodaje importer).
    /// </summary>
    public static ProvenanceInput Parse(Func<string, string> col, List<string> warnings)
    {
        // --- izvor ---
        string? source = null;
        var rawSource = col("izvor_podataka").Trim();
        if (rawSource.Length > 0)
        {
            if (SourceAlias.TryGetValue(Key(rawSource), out var s)) source = s;
            else warnings.Add($"izvor_podataka: nepoznata vrijednost „{rawSource}” — ignoriram (dozvoljeno: {string.Join(", ", DataSources)})");
        }

        // --- privola: status ---
        string? status = null;
        var rawStatus = col("privola_status").Trim();
        if (rawStatus.Length > 0)
        {
            if (StatusAlias.TryGetValue(Key(rawStatus), out var st)) status = st;
            else warnings.Add($"privola_status: nepoznata vrijednost „{rawStatus}” — ignoriram (dozvoljeno: nepoznato, zatraženo, dano, odbijeno)");
        }
        if (status == "refused") warnings.Add("privola odbijena — profil skriven (opt-out)");

        // --- privola: kanal ---
        string? channel = null;
        var rawChannel = col("privola_kanal").Trim();
        if (rawChannel.Length > 0)
        {
            if (ChannelAlias.TryGetValue(Key(rawChannel), out var ch)) channel = ch;
            else warnings.Add($"privola_kanal: nepoznata vrijednost „{rawChannel}” — ignoriram (dozvoljeno: {string.Join(", ", ConsentChannelsFromExcel)})");
        }

        // --- privola: opseg (više vrijednosti) ---
        List<string>? scope = null;
        var rawScope = col("privola_opseg").Trim();
        if (rawScope.Length > 0)
        {
            var found = new HashSet<string>();
            foreach (var part in rawScope.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (ScopeAlias.TryGetValue(Key(part), out var sc)) found.Add(sc);
                else warnings.Add($"privola_opseg: nepoznata vrijednost „{part}” — preskačem (dozvoljeno: podaci, slike, recenzije)");
            }
            if (found.Count > 0) scope = ConsentScopes.Where(found.Contains).ToList(); // kanonski redoslijed, bez duplikata
        }

        // --- datumi ---
        DateTime? Date(string column)
        {
            var raw = col(column).Trim();
            if (raw.Length == 0) return null;
            var d = ParseDate(raw);
            if (d == null) warnings.Add($"{column}: neispravan datum „{raw}” — ignoriram (npr. 30.09.2026. ili 2026-09-30)");
            return d;
        }
        var collected = Date("datum_prikupljanja");
        var requested = Date("privola_zatrazena");
        var consentAt = Date("privola_datum");

        // --- tekstualna ---
        var note = col("privola_napomena").Trim();
        var placeId = col("google_place_id").Trim();
        if (placeId.Length > 0 && placeId.Any(char.IsWhiteSpace))
            warnings.Add($"google_place_id: sadrži razmake („{placeId}”) — provjeri da je ID ispravno kopiran");

        return new ProvenanceInput(
            source, collected, status, requested, consentAt, channel, scope,
            note.Length == 0 ? null : note,
            placeId.Length == 0 ? null : placeId);
    }
}

/// <summary>
/// Primjena <see cref="ProvenanceInput"/> na pružatelja (§Zadatak 17). Čista logika nad entitetom — testabilna bez baze.
/// </summary>
public static class ProvenanceMerge
{
    /// <summary>
    /// <list type="bullet">
    /// <item><b>Prazno ne briše:</b> polje koje je u Excelu prazno (<c>null</c>) ne dira vrijednost u bazi (ručni unos admina ostaje).</item>
    /// <item><b><c>refused</c> → <c>OptOut = true</c>.</b> Importer NIKAD ne postavlja <c>OptOut = false</c> (vraćanje u prikaz radi samo admin).</item>
    /// <item><b>Privola preuzetog profila je zaštićena:</b> ako je profil <c>claimed</c>, a u bazi je <c>granted</c> s kanalom <c>claim</c>,
    /// Excel NE smije prepisati blok privole (status, kanal, datume, opseg) — ni na slabiji (<c>unknown/requested</c>) ni na drugačiji
    /// <c>granted</c>. Samo <c>refused</c> ima prednost. Ostala polja (izvor, datum prikupljanja, napomena, Place ID) se i dalje primjenjuju.</item>
    /// </list>
    /// Vraća upozorenja o zaštiti (bez broja retka — dodaje ih importer); prazno kad nema što prijaviti.
    /// </summary>
    public static IReadOnlyList<string> Apply(Vendor target, ProvenanceInput input)
    {
        var warnings = new List<string>();

        if (input.DataSource != null) target.DataSource = input.DataSource;
        if (input.DataCollectedAt != null) target.DataCollectedAt = input.DataCollectedAt;
        if (input.ConsentNote != null) target.ConsentNote = input.ConsentNote;
        if (input.GooglePlaceId != null) target.GooglePlaceId = input.GooglePlaceId;

        var refused = input.ConsentStatus == "refused";
        var protectedByClaim = target.ClaimStatus == "claimed" && target.ConsentStatus == "granted" && target.ConsentChannel == "claim";

        if (protectedByClaim && !refused)
        {
            if (ConsentWouldChange(target, input))
                warnings.Add("privola preuzetog profila (claim) nije prepisana iz Excela");
        }
        else
        {
            if (input.ConsentStatus != null) target.ConsentStatus = input.ConsentStatus;
            if (input.ConsentRequestedAt != null) target.ConsentRequestedAt = input.ConsentRequestedAt;
            if (input.ConsentAt != null) target.ConsentAt = input.ConsentAt;
            if (input.ConsentChannel != null) target.ConsentChannel = input.ConsentChannel;
            if (input.ConsentScope != null) target.ConsentScope = new List<string>(input.ConsentScope);
        }

        if (refused) target.OptOut = true; // nikad false

        return warnings;
    }

    private static bool ConsentWouldChange(Vendor v, ProvenanceInput i) =>
        (i.ConsentStatus != null && i.ConsentStatus != v.ConsentStatus)
        || (i.ConsentRequestedAt != null && i.ConsentRequestedAt != v.ConsentRequestedAt)
        || (i.ConsentAt != null && i.ConsentAt != v.ConsentAt)
        || (i.ConsentChannel != null && i.ConsentChannel != v.ConsentChannel)
        || (i.ConsentScope != null && !i.ConsentScope.SequenceEqual(v.ConsentScope ?? new List<string>()));
}
