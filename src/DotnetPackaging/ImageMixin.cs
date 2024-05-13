using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DotnetPackaging;

public static class ImageMixin
{
    public static Image Resize(this Image image, int width, int height)
    {
        image.Mutate(x => x.Resize(width, height));
        return image;
    }

    public static bool HasProperIconSize(this Image image)
    {
        return image.Height == image.Width && MathMixin.IsPowerOf2(image.Width);
    }
    
    public static Image Iconize(this Image originalImage)
    {
        // Determina la mayor dimensión de la imagen
        int maxDimension = Math.Max(originalImage.Width, originalImage.Height);

        // Encuentra la potencia de dos más cercana que sea mayor o igual a la mayor dimensión
        int size = MathMixin.NextPowerOfTwo(maxDimension);

        // Calcula el factor de escala para ajustar la imagen original al tamaño de la potencia de dos
        float scaleFactor = Math.Min((float)size / originalImage.Width, (float)size / originalImage.Height);

        // Crea una imagen con el tamaño escalado proporcionalmente
        var resizedImage = originalImage.Clone(ctx => ctx.Resize((int)(originalImage.Width * scaleFactor), (int)(originalImage.Height * scaleFactor)));

        // Crea una nueva imagen cuadrada con el fondo transparente o de otro color
        var paddedImage = new Image<Rgba32>(size, size);

        // Calcula las posiciones para centrar la imagen escalada en el lienzo cuadrado
        int x = (size - resizedImage.Width) / 2;
        int y = (size - resizedImage.Height) / 2;

        // Dibuja la imagen escalada en el centro del lienzo cuadrado
        paddedImage.Mutate(ctx => ctx.DrawImage(resizedImage, new Point(x, y), 1f));

        return paddedImage;
    }
}