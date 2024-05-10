using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Zafiro.Reactive;

namespace DotnetPackaging;

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


    public static bool HasProperIconSize(this Image image)
    {
        return image.Height == image.Width && MathMixin.IsPowerOf2(image.Width);
    }
    
    public static Image Iconize(this Image image)
    {
        int maxSize = Math.Max(image.Width, image.Height);
        var sideSide = MathMixin.NextPowerOfTwo(maxSize);
        var newSize = new Size(sideSide, sideSide);

        if (image.Size == newSize)
        {
            return image;
        }
        
        var newCanvas = new Image<Rgba32>(newSize.Width, newSize.Height, Color.Transparent);
        // Establecer el fondo transparente

        // Calcular las posiciones para centrar la imagen original en el nuevo lienzo
        int offsetX = (newSize.Width - image.Width) / 2;
        int offsetY = (newSize.Height - image.Height) / 2;

        // Dibujar la imagen original sobre el nuevo lienzo en la posición calculada
        newCanvas.Mutate(ctx => ctx.DrawImage(image, new Point(offsetX, offsetY), 1f));
        return newCanvas;
    }
}