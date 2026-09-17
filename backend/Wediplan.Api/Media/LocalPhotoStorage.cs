using Microsoft.Extensions.Options;

namespace Wediplan.Api.Media;

/// <summary>
/// Lokalna pohrana za dev/self-host: piše u <c>{ContentRoot}/wwwroot/uploads</c>, a servira se
/// preko <c>UseStaticFiles</c> na putanji <c>/uploads/...</c>. Javni URL = (PublicBaseUrl ?? "") + "/uploads/" + key.
/// </summary>
public class LocalPhotoStorage : IPhotoStorage
{
    private readonly string _root;
    private readonly string _publicBase;

    public LocalPhotoStorage(IWebHostEnvironment env, IOptions<StorageOptions> opt)
    {
        _root = Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(_root);
        _publicBase = (opt.Value.PublicBaseUrl ?? "").TrimEnd('/');
    }

    public async Task<string> SaveAsync(string objectKey, byte[] bytes, string contentType, CancellationToken ct = default)
    {
        var safe = Sanitize(objectKey);
        var full = Path.Combine(_root, safe.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, bytes, ct);
        return $"{_publicBase}/uploads/{safe}";
    }

    public Task DeleteAsync(string publicUrl, CancellationToken ct = default)
    {
        var marker = "/uploads/";
        var i = publicUrl.IndexOf(marker, StringComparison.Ordinal);
        if (i >= 0)
        {
            var key = Sanitize(publicUrl[(i + marker.Length)..]);
            var full = Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full)) File.Delete(full);
        }
        return Task.CompletedTask;
    }

    /// <summary>Spriječi izlazak iz uploads foldera (path traversal).</summary>
    private static string Sanitize(string key) =>
        string.Join('/', key.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(seg => seg != "." && seg != ".."));
}
