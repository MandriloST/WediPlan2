using Microsoft.AspNetCore.Mvc;
using Wediplan.Api.Data;

namespace Wediplan.Api.Controllers;

/// <summary>
/// GET /health — status API-ja i konekcije na bazu.
/// Koristi se za docker compose healthcheck, uptime monitoring (faza 5) i ručnu provjeru.
/// </summary>
[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromServices] AppDbContext db, CancellationToken ct)
    {
        bool dbOk;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            dbOk = await db.Database.CanConnectAsync(timeout.Token);
        }
        catch
        {
            dbOk = false;
        }

        return Ok(new
        {
            status = "ok",
            db = dbOk ? "ok" : "unavailable",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
        });
    }
}
