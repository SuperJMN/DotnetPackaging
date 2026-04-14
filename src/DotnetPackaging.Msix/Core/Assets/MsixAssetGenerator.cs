using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Msix.Core.Assets;

/// <summary>
/// Generates MSIX visual assets required by the Store from a single source icon.
/// All assets are placed under Assets\ with standard names.
/// </summary>
internal static class MsixAssetGenerator
{
    public record AssetSpec(string RelativePath, int Width, int Height);

    public static readonly IReadOnlyList<AssetSpec> RequiredAssets = new[]
    {
        new AssetSpec(@"Assets\StoreLogo.png", 50, 50),
        new AssetSpec(@"Assets\Square44x44Logo.png", 44, 44),
        new AssetSpec(@"Assets\Square150x150Logo.png", 150, 150),
        new AssetSpec(@"Assets\Wide310x150Logo.png", 310, 150),
        new AssetSpec(@"Assets\Square310x310Logo.png", 310, 310),
        new AssetSpec(@"Assets\SplashScreen.png", 620, 300),
    };

    public static Result<IReadOnlyDictionary<string, byte[]>> Generate(byte[] sourceIcon)
    {
        return Result.Try(() =>
        {
            using var source = Image.Load<Rgba32>(sourceIcon);
            var assets = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var spec in RequiredAssets)
            {
                assets[spec.RelativePath] = RenderAsset(source, spec.Width, spec.Height);
            }

            return (IReadOnlyDictionary<string, byte[]>)assets;
        });
    }

    private static byte[] RenderAsset(Image<Rgba32> source, int targetWidth, int targetHeight)
    {
        using var canvas = new Image<Rgba32>(targetWidth, targetHeight);

        // Fit the icon within the target bounds preserving aspect ratio
        float scale = Math.Min((float)targetWidth / source.Width, (float)targetHeight / source.Height);
        int scaledW = Math.Max(1, (int)(source.Width * scale));
        int scaledH = Math.Max(1, (int)(source.Height * scale));

        using var resized = source.Clone(ctx => ctx.Resize(scaledW, scaledH));

        int offsetX = (targetWidth - scaledW) / 2;
        int offsetY = (targetHeight - scaledH) / 2;

        canvas.Mutate(ctx => ctx.DrawImage(resized, new Point(offsetX, offsetY), 1f));

        using var ms = new MemoryStream();
        canvas.SaveAsPng(ms);
        return ms.ToArray();
    }
}
