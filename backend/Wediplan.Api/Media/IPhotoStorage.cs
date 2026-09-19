namespace Wediplan.Api.Media;

/// <summary>
/// Apstrakcija pohrane fotografija (Faza 5, §2.3). Dvije implementacije:
///   • LocalPhotoStorage — dev/self-host (piše u wwwroot/uploads, servira static files),
///   • R2PhotoStorage — produkcija (Cloudflare R2 preko S3 API-ja).
/// <c>objectKey</c> je putanja unutar spremišta, npr. "vendors/{vendorId}/{guid}.webp".
/// <c>SaveAsync</c> vraća JAVNI URL koji se sprema u <c>VendorPhoto.StorageKey</c> i vraća frontendu.
/// </summary>
public interface IPhotoStorage
{
    Task<string> SaveAsync(string objectKey, byte[] bytes, string contentType, CancellationToken ct = default);

    /// <summary>Briše po javnom URL-u (onome što je spremljeno u StorageKey). Tiho ignorira nepostojeće.</summary>
    Task DeleteAsync(string publicUrl, CancellationToken ct = default);
}

/// <summary>
/// Vezuje se na sekciju "Storage" u konfiguraciji. Ako je <c>R2:Bucket</c> postavljen → R2,
/// inače lokalna pohrana. <c>PublicBaseUrl</c> je bazni URL za javni pristup (CDN/custom domena
/// za R2; za lokalno prazno → servira se s "/uploads").
/// </summary>
public class StorageOptions
{
    public const string Section = "Storage";

    public string? PublicBaseUrl { get; set; }   // npr. "https://cdn.wediplan.hr" (bez završnog /)
    public R2Options R2 { get; set; } = new();

    // Obrada slika
    public int MaxUploadBytes { get; set; } = 10 * 1024 * 1024; // 10 MB
    public int MainMaxWidth { get; set; } = 1600;
    public int ThumbWidth { get; set; } = 400;
    public int MaxPhotosPerVendor { get; set; } = 12;

    // Žig (opcionalno): ako su oba postavljena, crta se tekstualni žig na glavnoj varijanti.
    public string? WatermarkText { get; set; }
    public string? WatermarkFontPath { get; set; } // .ttf/.otf; ako nema → žig se preskače

    public bool UseR2 => !string.IsNullOrWhiteSpace(R2.Bucket);
}

public class R2Options
{
    public string? Bucket { get; set; }
    public string? AccountId { get; set; }        // Cloudflare account id → endpoint
    public string? ServiceUrl { get; set; }        // alternativa AccountId-u (pun endpoint)
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
}
