using Wediplan.Api.Domain;

namespace Wediplan.Api.Import;

/// <summary>Ishod spajanja uvezenih recenzija jednog pružatelja (v. <see cref="ImportedReviewMerge.Apply"/>).</summary>
public sealed record ReviewMergeResult(
    IReadOnlyList<ImportedReview> ToAdd,
    IReadOnlyList<ImportedReview> ToRemove,
    int Kept,
    int RatingUpdated,
    int DuplicatesInExcel);

/// <summary>
/// Upsert uvezenih recenzija umjesto "obriši sve pa dodaj" (§Zadatak 16, PLAN-PRIORITETI-LANSIRANJE-3.md).
/// Prije je svaki ponovni Excel uvoz brisao i ponovno stvarao sve recenzije, pa bi se status provjere (koju je admin
/// obavio nad konkretnim screenshotom) izgubio, a dnevnik promjena punio šumom. Čista logika nad listama (bez baze
/// i bez Excela) — pa je testabilna, kao <see cref="ImportMerge"/> u Zadatku 10.
/// </summary>
public static class ImportedReviewMerge
{
    /// <summary>
    /// Spoji recenzije iz Excela (<paramref name="incoming"/>) s onima u bazi (<paramref name="existing"/>) za pružatelja <paramref name="vendorSlug"/>.
    /// <list type="bullet">
    /// <item><b>Isti ključ</b> (<see cref="ImportRules.ReviewKey"/>) → postojeći redak ostaje; status provjere se NE dira.
    /// Ažurira se samo <c>Rating</c> ako se razlikuje (+ <c>UpdatedAt</c>).</item>
    /// <item><b>Novi ključ</b> → novi redak: <c>ExternalKey</c>, <c>CreatedAt</c>, <c>VerificationStatus = "unverified"</c>.</item>
    /// <item><b>Postojeće kojih nema u Excelu</b> → za brisanje (Excel je izvor istine; dnevnik promjena bilježi brisanje).</item>
    /// <item><b>Stari retci bez ključa</b> (prije migracije) → ključ se izračuna iz njihovih polja i upari; pronađeni dobiju ključ (backfill).</item>
    /// <item><b>Duplikati u Excelu</b> (isti ključ) → zadržava se prvi, ostali se broje u <see cref="ReviewMergeResult.DuplicatesInExcel"/>
    /// (jedinstveni indeks <c>(vendor_id, external_key)</c> ne dopušta dva ista).</item>
    /// </list>
    /// Metoda MIJENJA prosljeđene objekte (ključ/rating/vrijeme) ali ne dodaje ni briše ništa u bazi — to radi pozivatelj.
    /// </summary>
    public static ReviewMergeResult Apply(
        string vendorSlug, IEnumerable<ImportedReview> existing, IEnumerable<ImportedReview> incoming, DateTime now)
    {
        // 1) dolazne: ključ + zadržavanje redoslijeda iz Excela; duplikati se preskaču
        var wanted = new List<(string Key, ImportedReview Review)>();
        var seen = new HashSet<string>();
        var duplicates = 0;
        foreach (var r in incoming)
        {
            var key = ImportRules.ReviewKey(vendorSlug, r.Author, r.Text, r.Source, r.Year);
            if (!seen.Add(key)) { duplicates++; continue; }
            r.ExternalKey = key;
            wanted.Add((key, r));
        }

        // 2) postojeće po ključu. Kad ih je više s istim ključem (npr. stari retci), zadržava se onaj s NAJNAPREDNIJIM
        //    statusom provjere (admin rad ima prednost); OrderBy je stabilan. Višak ide u brisanje.
        var matchable = new Dictionary<string, ImportedReview>();
        var toRemove = new List<ImportedReview>();
        foreach (var e in existing.OrderBy(x => x.VerificationStatus == "unverified" ? 1 : 0))
        {
            var key = e.ExternalKey ?? ImportRules.ReviewKey(vendorSlug, e.Author, e.Text, e.Source, e.Year);
            if (seen.Contains(key) && !matchable.ContainsKey(key)) matchable[key] = e;
            else toRemove.Add(e); // nema ga u Excelu ILI je dupli postojeći
        }

        // 3) spajanje
        var toAdd = new List<ImportedReview>();
        int kept = 0, ratingUpdated = 0;
        foreach (var (key, r) in wanted)
        {
            if (matchable.TryGetValue(key, out var e))
            {
                kept++;
                e.ExternalKey ??= key; // backfill starih redaka
                if (e.Rating != r.Rating)
                {
                    e.Rating = r.Rating;
                    e.UpdatedAt = now;
                    ratingUpdated++;
                }
            }
            else
            {
                r.CreatedAt = now;
                r.VerificationStatus = "unverified";
                toAdd.Add(r);
            }
        }

        return new ReviewMergeResult(toAdd, toRemove, kept, ratingUpdated, duplicates);
    }
}
