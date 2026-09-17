using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Contracts;
using Wediplan.Api.Data;

namespace Wediplan.Api.Controllers;

/// <summary>
/// GDPR opt-out (§9, faza 6). Pružatelj koji je uvezen bez svoje prijave može zatražiti da se
/// njegov profil skine iz javnog prikaza. Postavlja <c>Vendor.OptOut = true</c> ODMAH (pravo na
/// uklanjanje), a `.Published()` filtar tada isključuje profil iz svih javnih upita.
///
/// Zloupotreba (konkurent skida tuđi profil) je moguća jer je forma anonimna — zato je akcija
/// REVERZIBILNA: admin vidi popis skrivenih (`GET /api/admin/optouts`) i može vratiti profil
/// (`POST /api/admin/vendors/{slug}/restore-optout`). Ako se zloupotreba pojavi, prijeći na
/// model s odobrenjem (spremati zahtjev kao pending pa admin potvrđuje). Rate-limited.
/// </summary>
[ApiController]
[Route("api/optout")]
[EnableRateLimiting("lists")]
public class OptOutController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<OptOutController> _log;
    public OptOutController(AppDbContext db, ILogger<OptOutController> log) { _db = db; _log = log; }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] OptOutRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Slug)) return BadRequest(new { error = "slug_required" });

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Slug == req.Slug, ct);
        if (vendor == null) return NotFound(new { error = "vendor_not_found" });

        if (!vendor.OptOut)
        {
            vendor.OptOut = true;
            await _db.SaveChangesAsync(ct);
            // Audit u log (razlog/kontakt se ne perzistiraju u bazi radi minimizacije podataka).
            _log.LogWarning("GDPR opt-out: vendor={Slug} razlog={Reason} kontakt={Contact}",
                vendor.Slug, req.Reason ?? "(nema)", req.Contact ?? "(nema)");
        }
        return Ok(new { ok = true });
    }
}
