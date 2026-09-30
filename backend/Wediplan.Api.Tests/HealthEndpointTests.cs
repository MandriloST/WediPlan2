using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wediplan.Api.Data;
using Xunit;

namespace Wediplan.Api.Tests;

/// <summary>
/// Smoke test (§ Plan prioriteti #1b): diže CIJELU aplikaciju (Program.cs — DI, middleware pipeline,
/// rate limiter, Identity/cookie postavke, ForwardedHeaders grana, seed rola) preko
/// <see cref="WebApplicationFactory{TEntryPoint}"/> i pogađa <c>/api/health</c>.
///
/// Namjerno hermetičan — nema pravog Postgresa (zamjenjuje <c>AppDbContext</c> s EF InMemory), pa je
/// brz i deterministički i u CI-ju i lokalno. Svrha nije provjeriti je li baza dostupna (to je posao
/// health-checka u produkciji) nego da BILO KOJA greška u konfiguraciji aplikacije (npr. loše
/// postavljen <c>ForwardedHeadersOptions</c>, DI koji ne može razriješiti ovisnost i sl.) obori build
/// odmah — baš ono zbog čega Zadatak 1 postoji.
/// </summary>
public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Ukloni pravu (Npgsql) registraciju iz Program.cs i zamijeni je InMemory bazom —
                // isti obrazac kao u service-testovima niže (v. ReviewsControllerTests).
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("health-smoke-test"));
            });
        });
    }

    [Fact]
    public async Task Health_ReturnsOk_WhenAppBootsSuccessfully()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        // InMemory "baza" je uvijek dostupna, pa je 200 očekivan ishod KAD app ispravno starta.
        // Da je npr. ForwardedHeadersOptions (ili bilo koja druga DI/middleware postavka) loša,
        // CreateClient()/GetAsync bi bacio iznimku prije nego što bismo uopće stigli do assert-a.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
