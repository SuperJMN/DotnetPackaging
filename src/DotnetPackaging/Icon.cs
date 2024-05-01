using DotnetPackaging.Deb;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace DotnetPackaging;

public class Icon : IIcon
{
    private readonly ByteArrayByteProvider byteArrayByteProvider;

    private Icon(ByteArrayByteProvider byteArrayByteProvider, int sizeWidth)
    {
        this.byteArrayByteProvider = byteArrayByteProvider;
        Size = sizeWidth;
    }

    public static async Task<IIcon> FromImage(Image image)
    {
        await using var memoryStream = new MemoryStream();
        var icon = image.MakeAppIcon();
        await icon.SaveAsync(memoryStream, PngFormat.Instance);
        return new Icon(new ByteArrayByteProvider(memoryStream.ToArray()), image.Size.Width);
    }

    public IObservable<byte[]> Bytes => byteArrayByteProvider.Bytes;
    public long Length => byteArrayByteProvider.Length;

    public int Size { get; }
}