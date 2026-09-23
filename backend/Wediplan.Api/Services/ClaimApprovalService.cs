using Microsoft.EntityFrameworkCore;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;

namespace Wediplan.Api.Services;

/// <summary>
/// Zajednička logika odobrenja claima (§Zadatak 5, PLAN-PRIORITETI-LANSIRANJE-2.md) — dijele je
/// <see cref="Wediplan.Api.Controllers.AdminController.ApproveClaim"/> (ručni klik) i auto-approve put u
/// <see cref="Wediplan.Api.Controllers.ClaimsController.Verify"/> (nakon potvrde e-maila). Jedan izvor
/// istine za nuspojave odobrenja (objava drafta, owner, odbijanje ostalih pending zahtjeva).
/// </summary>
public class ClaimApprovalService
{
    private readonly AppDbContext _db;
    public ClaimApprovalService(AppDbContext db) { _db = db; }

    /// <summary>
    /// Odobri claim: objavi draft (ako postoji) u živu verziju, postavi vendor.ClaimStatus/OwnerUserId,
    /// označi claim odobrenim i odbij ostale pending zahtjeve za istog pružatelja (jedan vlasnik).
    /// <paramref name="decidedBy"/> je <c>null</c> za sustavno (auto) odobrenje — admin klik prosljeđuje
    /// svoj Id.
    /// </summary>
    public async Task ApproveAsync(Claim claim, Vendor vendor, Guid? decidedBy, CancellationToken ct)
    {
        var draft = await _db.VendorDrafts.FirstOrDefaultAsync(d => d.VendorId == vendor.Id, ct);
        if (draft != null) ProviderMapper.ApplyToVendor(draft, vendor);

        vendor.ClaimStatus = "claimed";
        vendor.OwnerUserId = claim.UserId;
        vendor.UpdatedAt = DateTime.UtcNow;

        claim.Status = "approved";
        claim.DecidedBy = decidedBy;
        claim.DecidedAt = DateTime.UtcNow;

        // Ostali pending zahtjevi za istog pružatelja → odbijeni (jedan vlasnik).
        var others = await _db.Claims
            .Where(c => c.VendorId == vendor.Id && c.Id != claim.Id && c.Status == "pending")
            .ToListAsync(ct);
        foreach (var o in others) { o.Status = "rejected"; o.DecidedBy = decidedBy; o.DecidedAt = DateTime.UtcNow; }

        await _db.SaveChangesAsync(ct);
    }
}
