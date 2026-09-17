using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wediplan.Api.Contracts;
using Wediplan.Api.Data;
using Wediplan.Api.Domain;
using Wediplan.Api.Media;

namespace Wediplan.Api.Controllers;

/// <summary>
/// Upravljanje fotografijama pružatelja (Faza 5, §2.3, §6.5). Dozvoljeno SAMO odobrenom vlasniku
/// (claimed + owner). Upload prolazi kroz <see cref="ImagePipeline"/> (WebP + žig + thumbnail) i
/// pohranu (<see cref="IPhotoStorage"/> — lokalno u dev, R2 u produkciji).
/// </summary>
[ApiController]
[Route("api/provider/vendors/{slug}/photos")]
[Authorize]
public class PhotosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IPhotoStorage _storage;
    private readonly ImagePipeline _pipeline;
    private readonly StorageOptions _opt;

    public PhotosController(AppDbContext db, UserManager<AppUser> users, IPhotoStorage storage,
        ImagePipeline pipeline, IOptions<StorageOptions> opt)
    { _db = db; _users = users; _storage = storage; _pipeline = pipeline; _opt = opt.Value; }

    private Guid Uid() => Guid.Parse(_users.GetUserId(User)!);

    /// <summary>Vraća pružatelja ako je pozivatelj njegov odobreni vlasnik, inače null.</summary>
    private async Task<Vendor?> OwnedAsync(string slug, CancellationToken ct)
    {
        var uid = Uid();
        var v = await _db.Vendors.FirstOrDefaultAsync(x => x.Slug == slug, ct);
        return v != null && v.OwnerUserId == uid && v.ClaimStatus == "claimed" ? v : null;
    }

    /// <summary>POST — upload jedne fotografije (multipart, polje "file").</summary>
    [HttpPost]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<ActionResult<ProviderPhotoDto>> Upload(string slug, IFormFile? file, CancellationToken ct)
    {
        var vendor = await OwnedAsync(slug, ct);
        if (vendor == null) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(new { error = "no_file" });
        if (file.Length > _opt.MaxUploadBytes) return BadRequest(new { error = "file_too_large" });
        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "not_an_image" });

        var count = await _db.Set<VendorPhoto>().CountAsync(p => p.VendorId == vendor.Id, ct);
        if (count >= _opt.MaxPhotosPerVendor) return BadRequest(new { error = "too_many_photos" });

        byte[] mainBytes, thumbBytes;
        try
        {
            await using var s = file.OpenReadStream();
            (mainBytes, thumbBytes) = await _pipeline.ProcessAsync(s, ct);
        }
        catch { return BadRequest(new { error = "invalid_image" }); }

        var baseKey = $"vendors/{vendor.Id}/{Guid.NewGuid():N}";
        var mainUrl = await _storage.SaveAsync($"{baseKey}.webp", mainBytes, ImagePipeline.WebpContentType, ct);
        await _storage.SaveAsync($"{baseKey}_thumb.webp", thumbBytes, ImagePipeline.WebpContentType, ct);

        var maxOrder = count == 0 ? -1 : await _db.Set<VendorPhoto>()
            .Where(p => p.VendorId == vendor.Id).MaxAsync(p => (int?)p.SortOrder, ct) ?? -1;

        var photo = new VendorPhoto
        {
            VendorId = vendor.Id,
            StorageKey = mainUrl,
            SortOrder = maxOrder + 1,
            IsCover = count == 0, // prva fotografija je naslovna
        };
        _db.Add(photo);
        await _db.SaveChangesAsync(ct);

        return Ok(ProviderMapper.PhotoDto(photo));
    }

    /// <summary>DELETE {id} — obriši fotografiju (i thumbnail) iz pohrane i baze.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(string slug, Guid id, CancellationToken ct)
    {
        var vendor = await OwnedAsync(slug, ct);
        if (vendor == null) return Forbid();

        var photo = await _db.Set<VendorPhoto>().FirstOrDefaultAsync(p => p.Id == id && p.VendorId == vendor.Id, ct);
        if (photo == null) return NotFound();

        await _storage.DeleteAsync(photo.StorageKey, ct);
        await _storage.DeleteAsync(ProviderMapper.ThumbUrl(photo.StorageKey), ct);
        var wasCover = photo.IsCover;
        _db.Remove(photo);
        await _db.SaveChangesAsync(ct);

        // ako je obrisana naslovna → prva preostala postaje naslovna
        if (wasCover)
        {
            var first = await _db.Set<VendorPhoto>().Where(p => p.VendorId == vendor.Id)
                .OrderBy(p => p.SortOrder).FirstOrDefaultAsync(ct);
            if (first != null) { first.IsCover = true; await _db.SaveChangesAsync(ct); }
        }
        return NoContent();
    }

    /// <summary>PUT order — novi redoslijed + naslovna.</summary>
    [HttpPut("order")]
    public async Task<IActionResult> Reorder(string slug, [FromBody] PhotoOrderRequest req, CancellationToken ct)
    {
        var vendor = await OwnedAsync(slug, ct);
        if (vendor == null) return Forbid();

        var photos = await _db.Set<VendorPhoto>().Where(p => p.VendorId == vendor.Id).ToListAsync(ct);
        var order = req.OrderedIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        foreach (var p in photos)
        {
            if (order.TryGetValue(p.Id.ToString(), out var idx)) p.SortOrder = idx;
            p.IsCover = req.CoverId != null && p.Id.ToString() == req.CoverId;
        }
        // ako CoverId nije poslan/ne postoji, osiguraj da je barem jedna naslovna
        if (photos.Count > 0 && !photos.Any(p => p.IsCover))
            photos.OrderBy(p => p.SortOrder).First().IsCover = true;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
