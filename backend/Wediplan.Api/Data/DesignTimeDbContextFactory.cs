using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wediplan.Api.Data;

/// <summary>
/// Omogućuje `dotnet ef migrations add ...` / `dotnet ef database update` bez pokretanja
/// aplikacije. Connection string: env WEDIPLAN_DB ili default lokalni (docker compose: 5433).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("WEDIPLAN_DB")
            ?? "Host=localhost;Port=5433;Database=wediplan;Username=wediplan;Password=wediplan";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(cs)
            .Options;
        return new AppDbContext(options);
    }
}
