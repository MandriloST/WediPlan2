using Microsoft.Extensions.Options;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Wediplan.Api.Media;

/// <summary>
/// Obrada uploadane fotografije (Faza 5, §2.3): auto-orijentacija, smanjivanje (bez povećavanja),
/// WebP kompresija i — ako je konfiguriran font + tekst — diskretni žig na glavnoj varijanti.
/// Vraća glavnu (do MainMaxWidth) i thumbnail (ThumbWidth) varijantu kao WebP bajtove.
/// </summary>
public class ImagePipeline
{
    private readonly StorageOptions _o;
    private readonly Font? _watermarkFont;

    public ImagePipeline(IOptions<StorageOptions> opt)
    {
        _o = opt.Value;
        if (!string.IsNullOrWhiteSpace(_o.WatermarkText) &&
            !string.IsNullOrWhiteSpace(_o.WatermarkFontPath) &&
            File.Exists(_o.WatermarkFontPath))
        {
            var fonts = new FontCollection();
            var family = fonts.Add(_o.WatermarkFontPath);
            _watermarkFont = family.CreateFont(22f, FontStyle.Bold);
        }
    }

    public const string WebpContentType = "image/webp";

    public async Task<(byte[] Main, byte[] Thumb)> ProcessAsync(Stream input, CancellationToken ct = default)
    {
        using var image = await Image.LoadAsync(input, ct);
        image.Mutate(x => x.AutoOrient());

        // glavna varijanta
        using var main = image.Clone(x =>
        {
            if (image.Width > _o.MainMaxWidth) x.Resize(_o.MainMaxWidth, 0);
        });
        DrawWatermark(main);
        var mainBytes = await EncodeWebpAsync(main, 80, ct);

        // thumbnail
        using var thumb = image.Clone(x =>
        {
            if (image.Width > _o.ThumbWidth) x.Resize(_o.ThumbWidth, 0);
        });
        var thumbBytes = await EncodeWebpAsync(thumb, 72, ct);

        return (mainBytes, thumbBytes);
    }

    private void DrawWatermark(Image img)
    {
        if (_watermarkFont is null) return;
        var text = _o.WatermarkText!;
        var opts = new TextOptions(_watermarkFont);
        var size = TextMeasurer.MeasureSize(text, opts);
        const float pad = 14f;
        var x = Math.Max(pad, img.Width - size.Width - pad);
        var y = Math.Max(pad, img.Height - size.Height - pad);
        img.Mutate(ctx => ctx.DrawText(text, _watermarkFont, Color.FromRgba(255, 255, 255, 170), new PointF(x, y)));
    }

    private static async Task<byte[]> EncodeWebpAsync(Image img, int quality, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await img.SaveAsync(ms, new WebpEncoder { Quality = quality }, ct);
        return ms.ToArray();
    }
}
