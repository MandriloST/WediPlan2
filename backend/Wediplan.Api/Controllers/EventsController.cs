using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Controllers;

/// <summary>
/// POST /api/events — first-party analitika (§A). Agregatno, bez PII.
/// - batch max 20, whitelist event_name, tihi 204 (nikad ne ruši UX).
/// - IP se koristi SAMO za rate limit u memoriji; NE pohranjuje se.
/// - session_hash dolazi s klijenta (bez kolačića). DNT/GPC filtrira klijent.
/// </summary>
[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    // Katalog eventa v1 (§A) — proširenja se dodaju OVDJE pa u lib/analytics.ts.
    private static readonly HashSet<string> Whitelist = new()
    {
        "page_view", "search_performed", "vendor_viewed", "map_region_clicked",
        "map_pin_clicked", "compare_added", "compare_viewed", "budget_calculated",
        "outbound_click", "favorite_added",
    };

    private const int MaxBatch = 20;

    // Jednostavan in-memory rate limit: max 60 zahtjeva / 60 s po IP-u.
    private static readonly ConcurrentDictionary<string, (DateTime win, int n)> Buckets = new();
    private const int LimitPerMinute = 60;

    private readonly AppDbContext _db;
    public EventsController(AppDbContext db) => _db = db;

    public record IncomingEvent(string Name, string? SessionHash, string? Page, JsonElement? Props);

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] List<IncomingEvent>? batch, CancellationToken ct)
    {
        // Uvijek 204 — analitika ne smije rušiti UX ni otkrivati stanje.
        if (batch == null || batch.Count == 0) return NoContent();

        var ip = Wediplan.Api.Infrastructure.ClientIp.Get(HttpContext);
        if (!AllowRate(ip)) return NoContent();

        var now = DateTime.UtcNow;
        var toAdd = new List<Event>();
        foreach (var e in batch.Take(MaxBatch))
        {
            if (string.IsNullOrWhiteSpace(e.Name) || !Whitelist.Contains(e.Name)) continue;
            toAdd.Add(new Event
            {
                Ts = now,
                EventName = e.Name,
                SessionHash = Trunc(e.SessionHash, 64),
                Page = Trunc(e.Page, 300),
                Props = SafeProps(e.Props),
            });
        }
        if (toAdd.Count > 0)
        {
            _db.Events.AddRange(toAdd);
            try { await _db.SaveChangesAsync(ct); } catch { /* fail-silent */ }
        }
        return NoContent();
    }

    private const int MaxPropsLength = 1000;

    /// <summary>props mora biti JSON objekt razumne veličine (dimenzije za daily_stats) — inače se odbacuje.</summary>
    private static string? SafeProps(JsonElement? props)
    {
        if (!props.HasValue || props.Value.ValueKind != JsonValueKind.Object) return null;
        var raw = props.Value.GetRawText();
        return raw.Length <= MaxPropsLength ? raw : null;
    }

    private static bool AllowRate(string ip)
    {
        var now = DateTime.UtcNow;
        var b = Buckets.AddOrUpdate(ip,
            _ => (now, 1),
            (_, cur) => (now - cur.win) > TimeSpan.FromMinutes(1) ? (now, 1) : (cur.win, cur.n + 1));
        return b.n <= LimitPerMinute;
    }

    private static string? Trunc(string? s, int max) =>
        string.IsNullOrEmpty(s) ? null : (s.Length <= max ? s : s[..max]);
}
