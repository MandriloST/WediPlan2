using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Data;

namespace Wediplan.Api.Import;

/// <summary>
/// Dnevni rollup (§A): events → daily_stats. Reporti čitaju SAMO daily_stats.
/// Pokreće se noćnim cronom/systemd timerom: `dotnet run -- --rollup [YYYY-MM-DD]`.
/// Idempotentno: prvo obriše taj dan iz daily_stats pa ponovno agregira.
/// Dimenzije se izvlače iz props jsonb: category/region/slug.
/// </summary>
public static class Rollup
{
    public static async Task RunAsync(AppDbContext db, DateOnly day, CancellationToken ct)
    {
        // Npgsql 6+ odbija DateTime s Kind=Unspecified za timestamptz → eksplicitno UTC.
        // Dan se agregira po UTC-u (00:00–24:00 UTC); za HR (UTC+1/+2) to je dovoljno za
        // dnevne trendove, a izbjegava DST dvosmislenosti.
        var start = DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var end = start.AddDays(1);

        // Obriši postojeći agregat za taj dan (idempotencija)
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM daily_stats WHERE day = {0}", new object[] { day }, ct);

        // Agregiraj iz sirovih eventa (dimenzije iz props jsonb)
        var affected = await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO daily_stats (day, event_name, category_slug, region_slug, vendor_slug, count)
            SELECT {0}::date,
                   event_name,
                   COALESCE(LEFT(props->>'category', 64), '') AS category_slug,
                   COALESCE(LEFT(props->>'region', 32), '')   AS region_slug,
                   COALESCE(LEFT(props->>'slug', 120), '')    AS vendor_slug,
                   COUNT(*)                          AS count
            FROM events
            WHERE ts >= {1} AND ts < {2}
            GROUP BY event_name, category_slug, region_slug, vendor_slug",
            new object[] { day, start, end }, ct);

        Console.WriteLine($"Rollup {day:yyyy-MM-dd}: {affected} redaka u daily_stats.");
    }
}
