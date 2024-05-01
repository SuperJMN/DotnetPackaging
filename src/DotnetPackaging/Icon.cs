using DotnetPackaging.Deb;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace DotnetPackaging;

public class Icon : IIcon
{
    private readonly ByteArrayByteProvider byteArrayByteProvider;

    private Icon(ByteArrayByteProvider byteArrayByteProvider)
    {
        this.byteArrayByteProvider = byteArrayByteProvider;
    }

    public static async Task<IIcon> FromImage(Image image)
    {
        await using var memoryStream = new MemoryStream();
        await image.SaveAsync(memoryStream, PngFormat.Instance);
        return new Icon(new ByteArrayByteProvider(memoryStream.ToArray()));
    }

    public IObservable<byte[]> Bytes => byteArrayByteProvider.Bytes;
    public long Length => byteArrayByteProvider.Length;
}