using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using Zafiro.FileSystem;

namespace DotnetPackaging;

public class Icon : IIcon
{
    private readonly ByteArrayData byteArrayData;

    private Icon(ByteArrayData byteArrayData, int size)
    {
        this.byteArrayData = byteArrayData;
        Size = size;
    }

    public static async Task<IIcon> FromImage(Image image)
    {
        await using var memoryStream = new MemoryStream();
        var icon = image.MakeAppIcon();
        await icon.SaveAsync(memoryStream, PngFormat.Instance);
        return new Icon(new ByteArrayData(memoryStream.ToArray()), image.Size.Width);
    }

    public IObservable<byte[]> Bytes => byteArrayData.Bytes;
    public long Length => byteArrayData.Length;

    public int Size { get; }
}