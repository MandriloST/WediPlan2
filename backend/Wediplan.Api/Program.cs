using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Data;
using Wediplan.Api.Import;

var builder = WebApplication.CreateBuilder(args);

// JSON: camelCase + izostavljanje null polja (identično Next.js mocku; API.md ugovor).
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Prioritet: WEDIPLAN_DB (env) > appsettings ConnectionStrings:Default > lokalni default.
// Env NADJAČAVA appsettings da se ops-postavka lako mijenja bez rebuilda.
var connectionString = Environment.GetEnvironmentVariable("WEDIPLAN_DB")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5433;Database=wediplan;Username=wediplan;Password=wediplan";

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(connectionString, npg =>
        // Više Include kolekcija (Categories+Photos+…) → SplitQuery izbjegava kartezijev umnožak.
        npg.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

const string CorsPolicy = "frontend";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
        ?? new[] { "http://localhost:3000" })
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

// --- CLI način: `dotnet run -- --import <xlsx> [--dry-run] [--no-geocode] [--geocode-retry]` ---
if (args.Contains("--import"))
{
    await RunImportAsync(app, args);
    return;
}

// --- CLI način: `dotnet run -- --rollup [YYYY-MM-DD]` (noćni cron/systemd timer) ---
if (args.Contains("--rollup"))
{
    var dayArg = args.SkipWhile(a => a != "--rollup").Skip(1).FirstOrDefault();
    var day = DateOnly.TryParse(dayArg, out var d) ? d
        : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)); // default: jučer
    using var scope = app.Services.CreateScope();
    var rdb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!await EnsureDbAsync(rdb)) { Environment.ExitCode = 1; return; }
    await Wediplan.Api.Import.Rollup.RunAsync(rdb, day, CancellationToken.None);
    return;
}

app.UseCors(CorsPolicy);
app.MapControllers();
app.Run();

static async Task RunImportAsync(WebApplication app, string[] args)
{
    var path = args.SkipWhile(a => a != "--import").Skip(1).FirstOrDefault();
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
    {
        Console.Error.WriteLine("Upotreba: dotnet run -- --import <putanja.xlsx> [--dry-run] [--no-geocode] [--geocode-retry]");
        Environment.ExitCode = 1;
        return;
    }
    bool dryRun = args.Contains("--dry-run");
    bool noGeocode = args.Contains("--no-geocode");
    // Ponovno pokušaj gradove koji su ranije završili kao null u cacheu (nakon popravka upita).
    bool retryNegatives = args.Contains("--geocode-retry");

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Fail-fast: provjeri bazu PRIJE (dugog) geokodiranja.
    if (!dryRun && !await EnsureDbAsync(db)) { Environment.ExitCode = 1; return; }

    Geocoder? geocoder = noGeocode || dryRun
        ? null
        : new Geocoder(Path.Combine(Directory.GetCurrentDirectory(), "geocode-cache.json")) { RetryNegatives = retryNegatives };

    var importer = new ExcelImporter(db, geocoder, dryRun);
    await importer.RunAsync(path, CancellationToken.None);
}

// Provjeri da je baza dostupna I da su tablice tu (migracije primijenjene). Jasna poruka.
static async Task<bool> EnsureDbAsync(AppDbContext db)
{
    var cs = db.Database.GetConnectionString();
    try
    {
        if (!await db.Database.CanConnectAsync())
        {
            Console.Error.WriteLine("GREŠKA: ne mogu se spojiti na bazu.");
            Console.Error.WriteLine($"  Connection: {cs}");
            Console.Error.WriteLine("  Provjeri: (1) Postgres radi (npr. `docker compose up -d postgres` u backend/),");
            Console.Error.WriteLine("            (2) rola i baza 'wediplan' postoje, (3) port se poklapa (docker = 5433).");
            return false;
        }
        await db.Vendors.CountAsync(); // otkriva 'tablice ne postoje'
        return true;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("GREŠKA: baza je dostupna, ali shema nije spremna.");
        Console.Error.WriteLine($"  Connection: {cs}");
        Console.Error.WriteLine("  Vjerojatno migracije nisu primijenjene — pokreni: dotnet ef database update");
        Console.Error.WriteLine($"  Detalj: {ex.GetType().Name}: {ex.Message}");
        return false;
    }
}
