using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Wediplan.Api.Media;

/// <summary>
/// Produkcijska pohrana na Cloudflare R2 (S3-kompatibilno). Objekti su privatni u bucketu, a javno
/// se serviraju preko R2 public/custom domene → <c>PublicBaseUrl</c> (npr. https://cdn.wediplan.hr).
/// Javni URL = PublicBaseUrl + "/" + objectKey.
/// </summary>
public class R2PhotoStorage : IPhotoStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly string _publicBase;

    public R2PhotoStorage(IOptions<StorageOptions> opt)
    {
        var o = opt.Value;
        _bucket = o.R2.Bucket!;
        _publicBase = (o.PublicBaseUrl ?? "").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(_publicBase))
            throw new InvalidOperationException("Storage:PublicBaseUrl je obavezan uz R2 (javna CDN domena).");

        var serviceUrl = !string.IsNullOrWhiteSpace(o.R2.ServiceUrl)
            ? o.R2.ServiceUrl!
            : $"https://{o.R2.AccountId}.r2.cloudflarestorage.com";

        _s3 = new AmazonS3Client(o.R2.AccessKey, o.R2.SecretKey, new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true,          // R2 zahtijeva path-style
            // R2 nema regije — "auto" je dovoljno.
            AuthenticationRegion = "auto",
        });
    }

    public async Task<string> SaveAsync(string objectKey, byte[] bytes, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream(bytes);
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            InputStream = ms,
            ContentType = contentType,
            DisablePayloadSigning = true,   // R2 preporuka za PutObject
        }, ct);
        return $"{_publicBase}/{objectKey}";
    }

    public async Task DeleteAsync(string publicUrl, CancellationToken ct = default)
    {
        if (!publicUrl.StartsWith(_publicBase, StringComparison.Ordinal)) return;
        var key = publicUrl[_publicBase.Length..].TrimStart('/');
        if (key.Length == 0) return;
        await _s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = key }, ct);
    }
}
