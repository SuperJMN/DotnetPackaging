using Zafiro.IO;

namespace DotnetPackaging.Deb;

public static class ImageMixin
{
    public static IObservable<byte> EncodeAsObservable(this Image image)
    {
        var memoryStream = new MemoryStream();
        image.SaveAsPngAsync(memoryStream);
        return memoryStream.ToObservable();
    }

    public static Image Resize(this Image image, int width, int height)
    {
        image.Mutate(x => x.Resize(width, height));
        return image;
    }
}