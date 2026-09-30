using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wediplan.Api.Data;
using Xunit;

namespace Wediplan.Api.Tests;

/// <summary>
/// Provjerava da je nova stroža rate-limit politika (§Zadatak 9, PLAN-PRIORITETI-LANSIRANJE-2.md)
/// stvarno primijenjena kroz cijeli middleware pipeline — isti obrazac kao
/// <see cref="HealthEndpointTests"/> (puna app preko WebApplicationFactory, EF InMemory).
/// Ne testira poslovnu logiku login-a (ne postoji seed korisnik — očekuje se 401/400 za svaki
/// pojedinačni zahtjev), nego SAMO da nakon limita stiže 429 s Retry-After zaglavljem.
/// </summary>
public class RateLimitingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RateLimitingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("ratelimit-test"));
            });
        });
    }

    [Fact]
    public async Task AuthPolicy_Returns429WithRetryAfter_AfterLimitExceeded()
    {
        // "auth" politika (Program.cs): PermitLimit = 10 po IP-u/min. TestServer daje isti
        // (sintetički) RemoteIpAddress za sve zahtjeve iz istog HttpClient-a, pa svih 15 zahtjeva
        // ovdje pada u istu particiju — dovoljno da pouzdano probije limit unutar jednog testa.
        var client = _factory.CreateClient();
        var body = new { email = "nepostojeci@primjer.hr", password = "nije-bitno-za-ovaj-test" };

        HttpResponseMessage? last = null;
        var sawTooManyRequests = false;
        for (var i = 0; i < 15; i++)
        {
            last = await client.PostAsJsonAsync("/api/auth/login", body);
            if (last.StatusCode == HttpStatusCode.TooManyRequests) { sawTooManyRequests = true; break; }
        }

        Assert.True(sawTooManyRequests, "Očekivan barem jedan 429 nakon 15 brzih zahtjeva na 'auth' politiku (limit 10/min).");
        Assert.NotNull(last);
        Assert.True(last!.Headers.RetryAfter != null, "429 odgovor mora nositi Retry-After zaglavlje.");
    }

    [Fact]
    public async Task ListsPolicy_StillWorks_ForOrdinaryVendorListRequests()
    {
        // Kontrolni test: postojeća "lists" politika (60/min) NIJE dirana ovim zadatkom — nekoliko
        // običnih zahtjeva i dalje prolazi normalno (regresija bi ovdje bila 429 na prvi zahtjev).
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/vendors?pageSize=1");

        Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
