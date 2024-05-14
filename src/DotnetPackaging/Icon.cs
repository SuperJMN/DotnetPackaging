using CSharpFunctionalExtensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using Zafiro.DataModel;
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

    public static Task<Result<IIcon>> FromImage(Image image)
    {
        return Result.Try(async () =>
        {
            await using var memoryStream = new MemoryStream();
            var icon = image.Iconize();
            await icon.SaveAsync(memoryStream, PngFormat.Instance);
            return (IIcon)new Icon(new ByteArrayData(memoryStream.ToArray()), icon.Width);
        });
    }

    public IObservable<byte[]> Bytes => byteArrayData.Bytes;
    public long Length => byteArrayData.Length;

    public int Size { get; }

    public static Task<Result<IIcon>> FromData(IData data)
    {
        return FromImage(Image.Load(data.Bytes()));
    }
}