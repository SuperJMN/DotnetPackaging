using DotnetPackaging.Deb;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace DotnetPackaging;

public class Icon : IIcon
{
    private readonly ByteArrayObservableDataStream byteArrayObservableDataStream;

    private Icon(ByteArrayObservableDataStream byteArrayObservableDataStream, int sizeWidth)
    {
        this.byteArrayObservableDataStream = byteArrayObservableDataStream;
        Size = sizeWidth;
    }

    public static async Task<IIcon> FromImage(Image image)
    {
        await using var memoryStream = new MemoryStream();
        var icon = image.MakeAppIcon();
        await icon.SaveAsync(memoryStream, PngFormat.Instance);
        return new Icon(new ByteArrayObservableDataStream(memoryStream.ToArray()), image.Size.Width);
    }

    public IObservable<byte[]> Bytes => byteArrayObservableDataStream.Bytes;
    public long Length => byteArrayObservableDataStream.Length;

    public int Size { get; }
}