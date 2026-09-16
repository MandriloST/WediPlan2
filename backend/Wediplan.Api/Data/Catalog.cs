namespace Wediplan.Api.Data;

// ============================================================================
// Šifrarnici kategorija i regija — VJERAN PORT frontend `lib/data.ts` i
// `lib/budget.ts`. Slugovi su ugovor s URL-ovima/SEO-om — NE mijenjati bez
// istovremene izmjene u lib/data.ts. Ovo je izvor za /api/categories, /api/regions,
// /api/budget-defaults i za nazive u /api/suggest.
// ============================================================================

public record CategoryDef(string Slug, string Name, string Group, string? Short = null);

public record RegionDef(string Id, string Name, double[] Center, double[][] Bounds);

public static class Catalog
{
    public static readonly IReadOnlyList<CategoryDef> Categories = new List<CategoryDef>
    {
        new("restorani-i-sale", "Restorani i sale", "sala", "Dvorane"),
        new("konobe-i-prostori", "Konobe i prostori za proslavu", "sala", "Konobe"),
        new("najam-kuce", "Najam kuće za proslavu", "sala", "Kuće"),
        new("najam-satora", "Najam šatora i opreme", "sala", "Šatori"),
        new("catering", "Catering", "catering"),
        new("torte-i-kolaci", "Torte i kolači", "catering", "Torte"),
        new("foto-i-video", "Foto i Video", "foto", "Fotografi"),
        new("foto-kabine", "Audio, foto kabine i selfie mirror", "foto", "Foto kabine"),
        new("glazba-bendovi", "Glazba — bendovi", "glazba", "Bendovi"),
        new("dj", "DJ", "glazba"),
        new("glazba-za-crkvu", "Glazba za crkvu", "glazba", "Crkvena glazba"),
        new("harmonikasi", "Harmonikaši", "glazba"),
        new("cvijece-i-dekoracije", "Cvijeće, dekoracije i baloni", "ostalo", "Cvijeće"),
        new("vjencanice", "Vjenčanice i dodaci", "ostalo", "Vjenčanice"),
        new("muska-odijela", "Muška odijela i dodaci", "ostalo", "Odijela"),
        new("nakit-i-prstenje", "Nakit i prstenje", "ostalo", "Nakit"),
        new("frizerski-saloni", "Frizerski saloni", "ostalo", "Frizeri"),
        new("sminka-nokti", "Šminka, nokti i trepavice", "ostalo", "Šminka"),
        new("najam-limuzina", "Najam auta, limuzina i oldtimera", "ostalo", "Oldtimeri"),
        new("prijevoz-i-transferi", "Prijevoz i transferi", "ostalo", "Transferi"),
        new("organizatori", "Organizatori vjenčanja", "ostalo", "Organizatori"),
        new("pozivnice", "Pozivnice, zahvalnice i popis gostiju", "ostalo", "Pozivnice"),
        new("rasvjeta-i-razglas", "Rasvjeta, razglas i efekti", "ostalo", "Rasvjeta"),
        new("cuvanje-djece", "Čuvanje i animacija djece", "ostalo", "Animacija djece"),
        new("skole-plesa", "Škole plesa", "ostalo", "Ples"),
        new("bracna-putovanja", "Bračna putovanja", "ostalo", "Putovanja"),
        new("dodaci-za-djevojacke", "Dodaci za djevojačke i momačke", "ostalo", "Djevojačke"),
        new("reveri-i-dodaci", "Reveri, narukvice, podvezice i dodaci", "ostalo", "Reveri"),
        new("simbolicni-maticar", "Simbolični matičar", "ostalo", "Matičari"),
    };

    public static readonly Dictionary<string, CategoryDef> CategoryBySlug =
        Categories.ToDictionary(c => c.Slug);

    public static readonly IReadOnlyList<RegionDef> Regions = new List<RegionDef>
    {
        new("istra", "Istra", new[] { 13.9, 45.15 }, new[] { new[] { 13.4, 44.75 }, new[] { 14.4, 45.55 } }),
        new("kvarner", "Kvarner", new[] { 14.7, 44.9 }, new[] { new[] { 13.9, 44.2 }, new[] { 15.9, 45.7 } }),
        new("dalmacija", "Dalmacija", new[] { 16.4, 43.6 }, new[] { new[] { 14.5, 42.35 }, new[] { 18.6, 44.6 } }),
        new("zagreb", "Zagreb i okolica", new[] { 16.0, 45.8 }, new[] { new[] { 14.8, 44.9 }, new[] { 17.4, 46.6 } }),
        new("slavonija", "Slavonija", new[] { 18.0, 45.4 }, new[] { new[] { 16.8, 44.8 }, new[] { 19.5, 46.0 } }),
    };

    public static readonly Dictionary<string, RegionDef> RegionById =
        Regions.ToDictionary(r => r.Id);

    // Budžetske raspodjele (lib/budget.ts). "hr" je default kad regija nije zadana.
    public static readonly Dictionary<string, Dictionary<string, double>> BudgetShares = new()
    {
        ["hr"] = new() { ["sala"] = 0.40, ["catering"] = 0.25, ["foto"] = 0.15, ["glazba"] = 0.10, ["ostalo"] = 0.10 },
        ["dalmacija"] = new() { ["sala"] = 0.42, ["catering"] = 0.24, ["foto"] = 0.15, ["glazba"] = 0.09, ["ostalo"] = 0.10 },
        ["istra"] = new() { ["sala"] = 0.43, ["catering"] = 0.24, ["foto"] = 0.14, ["glazba"] = 0.09, ["ostalo"] = 0.10 },
        ["kvarner"] = new() { ["sala"] = 0.41, ["catering"] = 0.25, ["foto"] = 0.14, ["glazba"] = 0.10, ["ostalo"] = 0.10 },
        ["zagreb"] = new() { ["sala"] = 0.38, ["catering"] = 0.26, ["foto"] = 0.16, ["glazba"] = 0.10, ["ostalo"] = 0.10 },
        ["slavonija"] = new() { ["sala"] = 0.35, ["catering"] = 0.27, ["foto"] = 0.14, ["glazba"] = 0.14, ["ostalo"] = 0.10 },
    };

    public static Dictionary<string, double> SharesFor(string? region) =>
        region != null && BudgetShares.TryGetValue(region, out var s) ? s : BudgetShares["hr"];
}
