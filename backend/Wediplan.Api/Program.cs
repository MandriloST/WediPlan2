using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Data;
using Wediplan.Api.Import;
using Microsoft.AspNetCore.Identity;

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

// ---------------------------------------------------------------- Faza 3: Identity + auth
builder.Services.AddIdentityCore<Wediplan.Api.Domain.AppUser>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
        o.Password.RequireNonAlphanumeric = false; // dužina > složeni znakovi (NIST); 8+ je minimum
        o.Lockout.MaxFailedAccessAttempts = 8;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.SignIn.RequireConfirmedEmail = true;
    })
    .AddRoles<Wediplan.Api.Domain.AppRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Sesija = aplikacijski httpOnly cookie (NE JWT u localStorage — §5).
builder.Services.AddAuthentication(o =>
    {
        o.DefaultScheme = Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme;
        o.DefaultSignInScheme = Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme;
    })
    .AddCookie(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme, o =>
    {
        o.Cookie.Name = "wediplan.session";
        o.Cookie.HttpOnly = true;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        // SameSite=Lax radi za frontend↔API na istoj domeni (wediplan.hr / api.wediplan.hr)
        // i za lokalni razvoj na localhost (isti host, drugi port). Secure u produkciji (HTTPS).
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        if (!builder.Environment.IsDevelopment())
        {
            var domain = builder.Configuration["Auth:CookieDomain"]; // npr. ".wediplan.hr"
            if (!string.IsNullOrWhiteSpace(domain)) o.Cookie.Domain = domain;
        }
        // API vraća statusni kod umjesto redirecta na login stranicu
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    })
    .AddCookie(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme, o =>
    {
        o.Cookie.Name = "wediplan.external";
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    });

// Google OAuth — registrira se SAMO ako su ključevi postavljeni (inače rute vraćaju 404).
var googleId = builder.Configuration["Google:ClientId"];
var googleSecret = builder.Configuration["Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleId) && !string.IsNullOrWhiteSpace(googleSecret))
{
    builder.Services.AddAuthentication().AddGoogle(o =>
    {
        o.ClientId = googleId;
        o.ClientSecret = googleSecret;
        o.SignInScheme = Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme;
        o.CallbackPath = "/signin-google";
    });
}

builder.Services.AddAuthorization();

// Email: Resend ako je ključ postavljen, inače dev konzola (potpuni auth tok lokalno bez servisa).
builder.Services.AddHttpClient();
if (!string.IsNullOrWhiteSpace(builder.Configuration["Email:ResendApiKey"]))
    builder.Services.AddSingleton<Wediplan.Api.Auth.IEmailSender, Wediplan.Api.Auth.ResendEmailSender>();
else
    builder.Services.AddSingleton<Wediplan.Api.Auth.IEmailSender, Wediplan.Api.Auth.ConsoleEmailSender>();
builder.Services.AddScoped<Wediplan.Api.Auth.AuthEmails>();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Seed rola (couple/provider/admin) pri startu — idempotentno.
using (var scope = app.Services.CreateScope())
{
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<Wediplan.Api.Domain.AppRole>>();
    foreach (var r in Wediplan.Api.Domain.Roles.All)
        if (!await roleMgr.RoleExistsAsync(r))
            await roleMgr.CreateAsync(new Wediplan.Api.Domain.AppRole(r));
}

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
