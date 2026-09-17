using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Import;

/// <summary>
/// Uvoz iz Excel predloška u Postgres. Otporan na greške: uveze valjane retke,
/// preskoči neispravne uz izvještaj (za razliku od Node "sve ili ništa").
/// Idempotentan po slugu (ponovni import ažurira). Geokodira gradove bez koordinata.
/// Inozemni pružatelji (BiH/SI): auto-detekcija po gradu ili stupac `drzava`;
/// nemaju HR regiju (region_slug=""), pronalaze se preko coverage-a.
/// </summary>
public class ExcelImporter
{
    private readonly AppDbContext _db;
    private readonly Geocoder? _geocoder;
    private readonly bool _dryRun;
    private readonly List<string> _skipped = new();
    private readonly List<string> _warnings = new();

    public ExcelImporter(AppDbContext db, Geocoder? geocoder, bool dryRun)
    {
        _db = db; _geocoder = geocoder; _dryRun = dryRun;
    }

    private static string RegionName(string id) => id switch
    {
        "istra" => "Istra", "kvarner" => "Kvarner", "dalmacija" => "Dalmacija",
        "zagreb" => "Zagreb i okolica", "slavonija" => "Slavonija", _ => id,
    };

    public async Task RunAsync(string path, CancellationToken ct)
    {
        using var wb = new XLWorkbook(path);
        var sheet = wb.Worksheets.FirstOrDefault(w => w.Name is "Pružatelji" or "Pruzatelji")
            ?? throw new InvalidOperationException("Nedostaje list 'Pružatelji'.");

        var (rows, header) = ReadSheet(sheet);
        Func<IDictionary<string, string>, string, string> col =
            (row, name) => row.TryGetValue(name, out var v) ? v : "";

        var parsed = new List<Vendor>();
        var reviewsByName = new Dictionary<string, string>(); // norm(name) → slug
        var seenSlug = new Dictionary<string, int>();
        int hidden = 0, foreignCount = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            int rowNo = i + 2;
            var name = col(row, "naziv").Trim();
            if (name.Length == 0) continue;
            if (ImportRules.Norm(name).StartsWith("primjer")) continue;
            if (ImportRules.Norm(col(row, "status")) == "skriveno") { hidden++; continue; }

            var category = ImportRules.ResolveCategory(col(row, "kategorija"));
            if (category == null) { Skip(rowNo, name, $"nepoznata kategorija „{col(row, "kategorija")}”"); continue; }

            // Regija sjedišta ILI inozemni pružatelj (drzava stupac / BiH auto-detekcija)
            var cityRaw = col(row, "grad");
            var region = ImportRules.ResolveRegion(col(row, "regija"));
            var explicitCountry = ImportRules.DetectCountry(col(row, "drzava"), null); // samo stupac
            string country;
            if (explicitCountry != null && explicitCountry != "hr")
            {
                country = explicitCountry;
                if (region != null) Warn(rowNo, name, $"drzava={country}, regija se ignorira");
                region = "";
            }
            else if (region != null)
            {
                country = "hr";
            }
            else
            {
                var detected = ImportRules.DetectCountry(null, cityRaw);
                if (detected is "ba" or "si")
                {
                    country = detected; region = "";
                    Warn(rowNo, name, $"bez HR regije — prepoznat inozemni ({detected.ToUpperInvariant()}), grad „{ImportRules.CleanCity(cityRaw)}”");
                }
                else { Skip(rowNo, name, $"nepoznata/prazna regija „{col(row, "regija")}” (grad nije prepoznat kao BiH/SI)"); continue; }
            }

            var slug = ImportRules.Slugify(name);
            if (seenSlug.TryGetValue(slug, out var prev)) { Skip(rowNo, name, $"duplikat sluga s retkom {prev}"); continue; }
            seenSlug[slug] = rowNo;

            // dodatne kategorije (max 2, split ';')
            var extra = new List<string>();
            foreach (var partRaw in col(row, "dodatne_kategorije").Split(';'))
            {
                var part = partRaw.Trim();
                if (part.Length == 0) continue;
                var c = ImportRules.ResolveCategory(part);
                if (c == null) Warn(rowNo, name, $"dodatne_kategorije: nepoznata „{part}” — preskačem");
                else if (c == category) { /* ponavlja primarnu — preskoči tiho */ }
                else if (!extra.Contains(c)) extra.Add(c);
            }
            if (extra.Count > 2) { extra = extra.Take(2).ToList(); Warn(rowNo, name, "dodatne_kategorije: >2 — uzeto prve 2"); }

            var city = ImportRules.CleanCity(cityRaw);  // "Split / Zagreb" → "Split" (prikaz i geokod); vrijedi za sve države
            if (country != "hr" && ImportRules.Norm(city) is "bih" or "bosna i hercegovina") city = "";

            ImportRules.Coords? coords = null;
            var coordsRaw = col(row, "koordinate").Trim();
            if (coordsRaw.Length > 0)
            {
                coords = ImportRules.ParseCoords(coordsRaw);
                if (coords == null) Warn(rowNo, name, $"neispravne koordinate „{coordsRaw}” — ignoriram");
                else if (coords.OutOfCroatia) { Warn(rowNo, name, "koordinate izvan HR/BiH/SI okvira — ignoriram"); coords = null; }
            }

            string precision = coords != null ? "exact" : (city.Length > 0 ? "city" : "region");
            if (country == "hr" && ImportRules.VenueCategories.Contains(category) && (city.Length == 0 || coords == null))
                Warn(rowNo, name, "sala/konoba/kuća bez točnih koordinata — koristi se centroid grada (preporuka: dodati koordinate)");

            var (covAll, covIds, covUnknown) = ImportRules.ParseCoverage(col(row, "pokriva_regije"));
            foreach (var u in covUnknown) Warn(rowNo, name, $"pokriva_regije: nepoznata regija „{u}” — preskačem");

            // cijena — PRAZAN nacin_cijene → onRequest (odluka B2)
            var mode = ImportRules.Norm(col(row, "nacin_cijene"));
            double from = ParseNum(col(row, "cijena_od"));
            double to = ParseNum(col(row, "cijena_do"));
            string priceKind = "onRequest"; int? pFrom = null, pTo = null;
            if (mode.Length == 0 || mode.StartsWith("na upit")) { priceKind = "onRequest"; }
            else if (mode.StartsWith("po osobi"))
            {
                if (from <= 0 || to <= 0 || from >= to) Warn(rowNo, name, "po osobi bez ispravnog raspona → na upit");
                else { priceKind = "perPerson"; pFrom = (int)from; pTo = (int)to; }
            }
            else if (mode.StartsWith("od"))
            {
                if (from <= 0) Warn(rowNo, name, "„od” bez cijena_od → na upit");
                else { priceKind = "from"; pFrom = (int)from; }
            }
            else Warn(rowNo, name, $"nepoznat nacin_cijene „{col(row, "nacin_cijene")}” → na upit");

            double rating = ParseNum(col(row, "ocjena"));
            int reviewCount = (int)ParseNum(col(row, "broj_recenzija"));
            var ratingSource = col(row, "izvor_ocjene").Trim();
            if (rating > 0)
            {
                if (rating < 0 || rating > 5) { Warn(rowNo, name, $"ocjena {rating} izvan 0–5 → 0"); rating = 0; reviewCount = 0; }
                else { if (ratingSource.Length == 0) Warn(rowNo, name, "ocjena bez izvor_ocjene"); if (reviewCount == 0) reviewCount = 1; }
            }
            else reviewCount = 0;

            var v = new Vendor
            {
                Slug = slug,
                Name = name,
                CategorySlug = category,
                RegionSlug = region,
                Country = country,
                City = city,
                Lat = coords?.Lat,
                Lng = coords?.Lng,
                LocationPrecision = precision,
                CoverageAll = covAll,
                CoverageRegions = covIds,
                CoverageNote = Nullify(col(row, "pokrivanje_napomena")),
                PriceKind = priceKind,
                PriceFrom = pFrom,
                PriceTo = pTo,
                Rating = rating,
                ReviewCount = reviewCount,
                RatingSource = Nullify(ratingSource),
                Verified = ImportRules.YesNo(col(row, "provjereno")),
                LiveCalendar = ImportRules.YesNo(col(row, "kalendar_uzivo")),
                StyleTags = SplitList(col(row, "stil"), ','),
                About = Nullify(col(row, "o_pruzatelju")),
                Services = SplitList(col(row, "usluge"), ','),
                Website = Nullify(col(row, "web")),
                Phone = Nullify(col(row, "telefon")),
                Email = Nullify(col(row, "email")),
                SocialInstagram = ImportRules.SocialUrl(col(row, "instagram"), "instagram"),
                SocialFacebook = ImportRules.SocialUrl(col(row, "facebook"), "facebook"),
                IsPublished = true,
                ClaimStatus = "unclaimed",
            };
            if (country != "hr") foreignCount++;
            // M2M: primarna + dodatne
            v.Categories.Add(new VendorCategory { CategorySlug = category, IsPrimary = true });
            foreach (var e in extra) v.Categories.Add(new VendorCategory { CategorySlug = e, IsPrimary = false });

            parsed.Add(v);
            reviewsByName[ImportRules.Norm(name)] = slug;
        }

        // Recenzije
        var reviewSheet = wb.Worksheets.FirstOrDefault(w => w.Name == "Recenzije");
        var importedReviews = new List<(string slug, ImportedReview r)>();
        if (reviewSheet != null)
        {
            var (rRows, _) = ReadSheet(reviewSheet);
            for (int i = 0; i < rRows.Count; i++)
            {
                var row = rRows[i];
                var refName = col(row, "naziv_pruzatelja").Trim();
                if (refName.Length == 0 || ImportRules.Norm(refName).StartsWith("primjer")) continue;
                if (!reviewsByName.TryGetValue(ImportRules.Norm(refName), out var slug))
                { Warn(i + 2, refName, "Recenzije: pružatelj ne postoji na listu Pružatelji"); continue; }
                double rr = ParseNum(col(row, "ocjena"));
                var text = col(row, "tekst").Trim();
                var source = col(row, "izvor").Trim();
                if (rr < 1 || rr > 5 || text.Length == 0 || source.Length == 0)
                { Warn(i + 2, refName, "Recenzije: ocjena 1–5, tekst i izvor su obavezni"); continue; }
                importedReviews.Add((slug, new ImportedReview
                {
                    Author = Nullify(col(row, "autor")) ?? "Anonimno",
                    Rating = (int)rr, Text = text, Source = source,
                    Year = (int)(ParseNum(col(row, "godina")) is var y && y > 0 ? y : DateTime.UtcNow.Year),
                }));
            }
        }

        // Geokodiranje (grad bez koordinata) — po državi pružatelja
        int geocoded = 0;
        if (_geocoder != null && !_dryRun)
        {
            var need = parsed.Where(v => v.Lat == null && v.City.Length > 0).ToList();
            Console.WriteLine($"Geokodiram {need.Count} redaka (jedinstveni gradovi idu na mrežu ~1/s, ostalo cache)…");
            foreach (var v in need)
            {
                var ll = await _geocoder.GeocodeAsync(v.City, RegionName(v.RegionSlug), ct, v.Country);
                if (ll != null) { v.Lat = ll[0]; v.Lng = ll[1]; geocoded++; }
            }
            _geocoder.Save();
        }

        // Zapis u bazu (upsert po slugu)
        if (!_dryRun)
        {
            await UpsertAsync(parsed, importedReviews, ct);
        }

        // Izvještaj
        Console.WriteLine();
        Console.WriteLine($"{(_dryRun ? "[DRY RUN] " : "")}Valjanih pružatelja: {parsed.Count} (od toga inozemnih: {foreignCount})");
        Console.WriteLine($"Preskočeno (greške): {_skipped.Count} · Skriveno: {hidden} · Upozorenja: {_warnings.Count}");
        if (!_dryRun && _geocoder != null)
            Console.WriteLine($"Geokodirano sada: {geocoded} · cache hit: {_geocoder.Hits} · neuspjeh: {_geocoder.Failures}");
        Console.WriteLine($"Recenzije (valjane): {importedReviews.Count}");
        WriteReport();
    }

    private async Task UpsertAsync(List<Vendor> parsed,
        List<(string slug, ImportedReview r)> reviews, CancellationToken ct)
    {
        var reviewsBySlug = reviews.GroupBy(x => x.slug).ToDictionary(g => g.Key, g => g.Select(x => x.r).ToList());
        int inserted = 0, updated = 0;
        foreach (var v in parsed)
        {
            var existing = await _db.Vendors
                .Include(x => x.Categories).Include(x => x.ImportedReviews)
                .FirstOrDefaultAsync(x => x.Slug == v.Slug, ct);

            if (existing == null)
            {
                if (reviewsBySlug.TryGetValue(v.Slug, out var rv)) v.ImportedReviews = rv;
                _db.Vendors.Add(v);
                inserted++;
            }
            else
            {
                // ažuriraj skalarna polja
                existing.Name = v.Name; existing.CategorySlug = v.CategorySlug; existing.RegionSlug = v.RegionSlug;
                existing.Country = v.Country;
                existing.City = v.City; existing.Lat = v.Lat; existing.Lng = v.Lng;
                existing.LocationPrecision = v.LocationPrecision;
                existing.CoverageAll = v.CoverageAll; existing.CoverageRegions = v.CoverageRegions;
                existing.CoverageNote = v.CoverageNote;
                existing.PriceKind = v.PriceKind; existing.PriceFrom = v.PriceFrom; existing.PriceTo = v.PriceTo;
                existing.Rating = v.Rating; existing.ReviewCount = v.ReviewCount; existing.RatingSource = v.RatingSource;
                existing.Verified = v.Verified; existing.LiveCalendar = v.LiveCalendar;
                existing.StyleTags = v.StyleTags; existing.About = v.About; existing.Services = v.Services;
                existing.Website = v.Website; existing.Phone = v.Phone; existing.Email = v.Email;
                existing.SocialInstagram = v.SocialInstagram; existing.SocialFacebook = v.SocialFacebook;
                existing.UpdatedAt = DateTime.UtcNow;
                // zamijeni kategorije i recenzije
                _db.VendorCategories.RemoveRange(existing.Categories);
                existing.Categories = v.Categories;
                _db.ImportedReviews.RemoveRange(existing.ImportedReviews);
                existing.ImportedReviews = reviewsBySlug.TryGetValue(v.Slug, out var rv) ? rv : new();
                updated++;
            }
        }
        await _db.SaveChangesAsync(ct);
        Console.WriteLine($"Baza: novih {inserted}, ažurirano {updated}.");
    }

    // ---- pomoćne ----
    private void Skip(int rowNo, string name, string msg) => _skipped.Add($"red {rowNo} ({name}): {msg}");
    private void Warn(int rowNo, string name, string msg) => _warnings.Add($"red {rowNo} ({name}): {msg}");

    private static double ParseNum(string s) =>
        double.TryParse(s.Replace(",", "."), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static string? Nullify(string s) { s = s.Trim(); return s.Length == 0 ? null : s; }

    private static List<string> SplitList(string s, char sep) =>
        s.Split(sep).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

    private static (List<IDictionary<string, string>> rows, List<string> header) ReadSheet(IXLWorksheet sheet)
    {
        var rows = new List<IDictionary<string, string>>();
        var used = sheet.RangeUsed();
        if (used == null) return (rows, new());
        var headerRow = used.FirstRow();
        var header = headerRow.Cells().Select(c => ImportRules.Norm(c.GetString()).TrimEnd('*')).ToList();
        foreach (var r in used.Rows().Skip(1))
        {
            var dict = new Dictionary<string, string>();
            int idx = 0;
            foreach (var cell in r.Cells(1, header.Count))
            {
                if (idx < header.Count) dict[header[idx]] = cell.GetString();
                idx++;
            }
            rows.Add(dict);
        }
        return (rows, header);
    }

    private void WriteReport()
    {
        var lines = new List<string> { $"# Wediplan import izvještaj — {DateTime.UtcNow:u}", "", "## Preskočeni retci (greške)" };
        lines.AddRange(_skipped.Count > 0 ? _skipped : new List<string> { "(nema)" });
        lines.Add(""); lines.Add("## Upozorenja");
        lines.AddRange(_warnings.Count > 0 ? _warnings : new List<string> { "(nema)" });
        var p = Path.Combine(Directory.GetCurrentDirectory(), "import-report.txt");
        File.WriteAllText(p, string.Join("\n", lines));
        Console.WriteLine($"Izvještaj: {p}");
    }
}
