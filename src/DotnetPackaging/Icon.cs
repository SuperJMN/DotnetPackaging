using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using Zafiro.DataModel;
using Zafiro.FileSystem.Core;

namespace DotnetPackaging;

public class Icon : IIcon
{
    private readonly IData data;

    private Icon(IData data, int size)
    {
        this.data = data;
        Size = size;
    }

    public static Task<Result<IIcon>> FromImage(Image image)
    {
        return Result.Try(async () =>
        {
            await using var memoryStream = new MemoryStream();
            var icon = image.Iconize();
            await icon.SaveAsync(memoryStream, PngFormat.Instance);
            return (IIcon)new Icon(Data.FromByteArray(memoryStream.ToArray()), icon.Width);
        });
    }

    public IObservable<byte[]> Bytes => data.Bytes;
    public long Length => data.Length;

    public int Size { get; }

    public static Task<Result<IIcon>> FromData(IData data)
    {
        return FromImage(Image.Load(data.Bytes()));
    }
}