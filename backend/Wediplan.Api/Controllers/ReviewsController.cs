using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Contracts;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Controllers;

/// <summary>
/// Korisničke recenzije (§6, feature #4). Prijavljeni korisnik ostavlja ocjenu + tekst; recenzija
/// ide u <c>pending</c> (moderacija prije objave). Jedna recenzija po (korisnik, pružatelj).
/// Objavljene recenzije vraća profil (VendorsController.Get).
/// </summary>
[ApiController]
[Route("api/reviews")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    public ReviewsController(AppDbContext db, UserManager<AppUser> users) { _db = db; _users = users; }

    private Guid Uid() => Guid.Parse(_users.GetUserId(User)!);

    /// <summary>POST /api/reviews — nova recenzija (ide u moderaciju).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ReviewRequest req, CancellationToken ct)
    {
        var uid = Uid();
        var vendor = await _db.Vendors.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Slug == req.VendorSlug && v.IsPublished && !v.OptOut, ct);
        if (vendor == null) return NotFound(new { error = "vendor_not_found" });

        // Vlasnik ne recenzira sam sebe.
        if (vendor.OwnerUserId == uid) return BadRequest(new { error = "own_vendor" });

        var exists = await _db.UserReviews.AnyAsync(r => r.UserId == uid && r.VendorId == vendor.Id, ct);
        if (exists) return Conflict(new { error = "already_reviewed" });

        _db.UserReviews.Add(new UserReview
        {
            VendorId = vendor.Id, UserId = uid,
            Rating = Math.Clamp(req.Rating, 1, 5),
            Text = req.Text.Trim(),
            Status = "pending",
        });
        await _db.SaveChangesAsync(ct);

        return Ok(new { status = "pending", message = "Hvala! Recenzija čeka provjeru prije objave." });
    }
}
