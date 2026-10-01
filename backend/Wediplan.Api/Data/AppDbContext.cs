using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Data;

/// <summary>
/// Glavni DbContext. Od Faze 3 nasljeđuje IdentityDbContext (ASP.NET Core Identity nad Guid
/// ključem) — donosi tablice korisnika/rola/logina. Domenske tablice (vendors, events…) i
/// auth pomoćne tablice (magic_links…) su niže.
/// </summary>
public class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorCategory> VendorCategories => Set<VendorCategory>();
    public DbSet<VendorPhoto> VendorPhotos => Set<VendorPhoto>();
    public DbSet<ImportedReview> ImportedReviews => Set<ImportedReview>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<DailyStat> DailyStats => Set<DailyStat>();
    public DbSet<Sponsorship> Sponsorships => Set<Sponsorship>();

    // Zadatak 13 — dnevnik promjena (GDPR); puni ga interceptor (Zadatak 14)
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Faza 3 — auth pomoćne tablice
    public DbSet<MagicLink> MagicLinks => Set<MagicLink>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

    // Faza 3 (kraj) — couple podaci
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<BudgetPlan> BudgetPlans => Set<BudgetPlan>();

    // Faza 4 — claim, korisničke recenzije, draft uređivanja, pretplate
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimVerificationToken> ClaimVerificationTokens => Set<ClaimVerificationToken>();
    public DbSet<UserReview> UserReviews => Set<UserReview>();
    public DbSet<VendorDraft> VendorDrafts => Set<VendorDraft>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b); // OBAVEZNO prvo — konfigurira Identity tablice

        // pg_trgm za typeahead (tolerancija tipfelera) — §2.2. Postgres-specifično: pod drugim
        // providerom (npr. EF InMemory u testovima, § Plan prioriteti #1b) ovo se preskače, jer
        // te Npgsql-fluent-API pozive nema smisla (ni jamstvo da rade) izvan pravog Postgresa.
        var isNpgsql = Database.IsNpgsql();
        if (isNpgsql) b.HasPostgresExtension("pg_trgm");

        b.Entity<Vendor>(e =>
        {
            e.ToTable("vendors");
            e.HasKey(v => v.Id);
            e.HasIndex(v => v.Slug).IsUnique();
            e.HasIndex(v => v.CategorySlug);
            e.HasIndex(v => v.RegionSlug);
            e.Property(v => v.CoverageRegions).HasColumnType("text[]");
            e.Property(v => v.StyleTags).HasColumnType("text[]");
            e.Property(v => v.Services).HasColumnType("text[]");

            // Zadatak 13 — porijeklo podataka i privola. VAŽNO: mora biti PRIJE bloka "if (!isNpgsql) … return"
            // ispod (taj return preskače sve iza sebe pod drugim providerom).
            // HasDefaultValue → migracija postavlja vrijednost i na POSTOJEĆE retke (inače bi dobili "").
            e.Property(v => v.ConsentStatus).HasDefaultValue("unknown");
            e.Property(v => v.ConsentScope).HasColumnType("text[]");
            // Prazan niz kao DB default (robusnije od CLR defaulta u AddColumn za NOT NULL text[]); samo Npgsql.
            if (isNpgsql) e.Property(v => v.ConsentScope).HasDefaultValueSql("'{}'");
            e.HasIndex(v => v.GooglePlaceId); // nije unique: isti Place može imati 2 profila

            if (!isNpgsql)
            {
                // Search je NpgsqlTsVector? — CLR tip koji izvan Npgsqla EF ne zna mapirati kao
                // skalar (pokušava ga "razviti" kao complex/owned tip i puca na traženju
                // konstruktora: "No suitable constructor was found for entity type 'NpgsqlTsVector'").
                // Samo isključivanje generated-column/GIN konfiguracije (v. gore) NIJE dovoljno —
                // sam CLR tip stupca treba potpuno maknuti iz modela pod drugim providerom (testovi).
                e.Ignore(v => v.Search);
                return; // ostatak (GIN/trgm) je čisto Postgres — v. komentar gore
            }

            // Generirani tsvector iz name/city/about (§2.2). FTS relevancija;
            // typeahead ide preko pg_trgm (v. indekse dolje).
            e.HasGeneratedTsVectorColumn(
                    v => v.Search,
                    "simple",
                    v => new { v.Name, v.City, v.About })
                .HasIndex(v => v.Search)
                .HasMethod("GIN");

            // pg_trgm GIN indeksi za ILIKE/similarity na imenu i gradu
            e.HasIndex(v => v.Name).HasMethod("GIN").HasOperators("gin_trgm_ops");
            e.HasIndex(v => v.City).HasMethod("GIN").HasOperators("gin_trgm_ops");
        });

        b.Entity<VendorCategory>(e =>
        {
            e.ToTable("vendor_categories");
            e.HasKey(vc => new { vc.VendorId, vc.CategorySlug });
            e.HasIndex(vc => vc.CategorySlug); // brojači/filtri po svim kategorijama (§4.3)
            e.HasOne(vc => vc.Vendor).WithMany(v => v.Categories)
                .HasForeignKey(vc => vc.VendorId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<VendorPhoto>(e =>
        {
            e.ToTable("vendor_photos");
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.VendorId);
            // Zadatak 13 — evidencija i moderacija slika
            e.Property(p => p.Source).HasDefaultValue("partner");
            e.Property(p => p.ModerationStatus).HasDefaultValue("unreviewed");
            e.Property(p => p.ModerationNote).HasMaxLength(1000);
            e.HasIndex(p => p.ModerationStatus); // red za pregled: status = unreviewed
            e.HasOne(p => p.Vendor).WithMany(v => v.Photos)
                .HasForeignKey(p => p.VendorId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ImportedReview>(e =>
        {
            e.ToTable("imported_reviews");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.VendorId);
            // Zadatak 13 — stabilni ključ + provjera uvezenih recenzija
            e.Property(r => r.VerificationStatus).HasDefaultValue("unverified");
            e.Property(r => r.EvidenceNote).HasMaxLength(1000);
            if (isNpgsql)
                // Jedinstven ključ po pružatelju, ali samo za retke koji ga imaju (stari retci imaju NULL).
                e.HasIndex(r => new { r.VendorId, r.ExternalKey }).IsUnique().HasFilter("external_key IS NOT NULL");
            else
                e.HasIndex(r => new { r.VendorId, r.ExternalKey });
            e.HasOne(r => r.Vendor).WithMany(v => v.ImportedReviews)
                .HasForeignKey(r => r.VendorId).OnDelete(DeleteBehavior.Cascade);
        });

        // Zadatak 13 — dnevnik promjena. NAMJERNO bez FK-ova (mora preživjeti brisanje korisnika/entiteta).
        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_log");
            e.HasKey(x => x.Id);
            e.Property(x => x.Changes).HasColumnType("jsonb");
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAt }); // povijest jednog entiteta
            e.HasIndex(x => x.ActorUserId);
            e.HasIndex(x => x.OccurredAt); // retencija (--audit-prune)
        });

        b.Entity<Event>(e =>
        {
            e.ToTable("events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Props).HasColumnType("jsonb");
            e.HasIndex(x => x.Ts);
            e.HasIndex(x => new { x.EventName, x.Ts });
        });

        b.Entity<DailyStat>(e =>
        {
            e.ToTable("daily_stats");
            e.HasKey(x => new { x.Day, x.EventName, x.CategorySlug, x.RegionSlug, x.VendorSlug });
        });

        b.Entity<Sponsorship>(e =>
        {
            e.ToTable("sponsorships");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.VendorId);
        });

        // --- Faza 3: auth pomoćne tablice ---
        b.Entity<MagicLink>(e =>
        {
            e.ToTable("magic_links");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(320);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.Email); // rate-limit po emailu
        });

        b.Entity<EmailVerificationToken>(e =>
        {
            e.ToTable("email_verification_tokens");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
        });

        b.Entity<Favorite>(e =>
        {
            e.ToTable("favorites");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.VendorId }).IsUnique(); // jedan favorit po paru/pružatelju
            e.HasIndex(x => x.UserId);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            // NEMA FK na Vendor namjerno (v. Favorite komentar)
        });

        b.Entity<BudgetPlan>(e =>
        {
            e.ToTable("budget_plans");
            e.HasKey(x => x.UserId); // 1:1 s korisnikom
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- Faza 4: claim / recenzije / draft / pretplate ---
        b.Entity<Claim>(e =>
        {
            e.ToTable("claims");
            e.HasKey(x => x.Id);
            e.Property(x => x.Message).HasMaxLength(2000);
            e.HasIndex(x => x.VendorId);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.Status);
            // Jedan aktivan zahtjev po (korisnik, pružatelj): sprječava duple pending claimove.
            e.HasIndex(x => new { x.UserId, x.VendorId }).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserReview>(e =>
        {
            e.ToTable("user_reviews");
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(4000);
            e.Property(x => x.RejectReason).HasMaxLength(500); // Zadatak 13
            e.HasIndex(x => x.VendorId);
            e.HasIndex(x => new { x.VendorId, x.Status }); // profil čita samo published
            // Jedna recenzija po (korisnik, pružatelj).
            e.HasIndex(x => new { x.UserId, x.VendorId }).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Cascade);
        });

        // Dokaz vlasništva claima e-mailom (§Zadatak 5). FK cascade na Claim — briše li se
        // claim (npr. korisnik obriše račun → kaskadno preko AppUser), token nestaje s njim.
        b.Entity<ClaimVerificationToken>(e =>
        {
            e.ToTable("claim_verification_tokens");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.ClaimId);
            e.HasOne<Claim>().WithMany().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<VendorDraft>(e =>
        {
            e.ToTable("vendor_drafts");
            e.HasKey(x => x.VendorId); // 1:1 s pružateljem
            e.Property(x => x.Services).HasColumnType("text[]");
            e.Property(x => x.StyleTags).HasColumnType("text[]");
            e.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Subscription>(e =>
        {
            e.ToTable("subscriptions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.VendorId);
        });

        // Identity tablice u snake_case (default su AspNetUsers itd.). Radi konzistentnosti sa
        // ostatkom sheme; imena su stabilna jer migracije ionako fiksiraju shemu.
        b.Entity<AppUser>().ToTable("users");
        b.Entity<AppRole>().ToTable("roles");
        b.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        b.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        b.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        b.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        b.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        // snake_case za sve stupce (Postgres konvencija)
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
                prop.SetColumnName(ToSnake(prop.GetColumnName() ?? prop.Name));
    }

    private static string ToSnake(string name)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
