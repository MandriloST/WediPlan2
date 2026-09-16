using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorCategory> VendorCategories => Set<VendorCategory>();
    public DbSet<VendorPhoto> VendorPhotos => Set<VendorPhoto>();
    public DbSet<ImportedReview> ImportedReviews => Set<ImportedReview>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<DailyStat> DailyStats => Set<DailyStat>();
    public DbSet<Sponsorship> Sponsorships => Set<Sponsorship>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // pg_trgm za typeahead (tolerancija tipfelera) — §2.2
        b.HasPostgresExtension("pg_trgm");

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
            e.HasOne(p => p.Vendor).WithMany(v => v.Photos)
                .HasForeignKey(p => p.VendorId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ImportedReview>(e =>
        {
            e.ToTable("imported_reviews");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.VendorId);
            e.HasOne(r => r.Vendor).WithMany(v => v.ImportedReviews)
                .HasForeignKey(r => r.VendorId).OnDelete(DeleteBehavior.Cascade);
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
