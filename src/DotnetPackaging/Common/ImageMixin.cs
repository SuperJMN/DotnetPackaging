using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Zafiro.Reactive;

namespace DotnetPackaging.Common;

public static class ImageMixin
{
    public static IObservable<byte> EncodeAsObservable(this Image image)
    {
        var memoryStream = new MemoryStream();
        image.SaveAsPngAsync(memoryStream);
        memoryStream.Position = 0;
        return StreamMixin.ToObservable(memoryStream);
    }

    public static async Task<byte[]> ToBytes(this Image image)
    {
        using (var memoryStream = new MemoryStream())
        {
            await image.SaveAsPngAsync(memoryStream);
            return memoryStream.ToArray();
        }
    }

    public static Image Resize(this Image image, int width, int height)
    {
        image.Mutate(x => x.Resize(width, height));
        return image;
    }
}