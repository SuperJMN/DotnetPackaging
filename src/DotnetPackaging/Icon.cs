using System.Reactive.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using Zafiro.DivineBytes;

namespace DotnetPackaging;

public class Icon : IIcon
{
    private readonly IByteSource source;

    private Icon(IByteSource source, int size)
    {
        this.source = source;
        Size = size;
    }

    public static Task<Result<IIcon>> FromImage(Image image)
    {
        return Result.Try(async () =>
        {
            await using var memoryStream = new MemoryStream();
            var icon = image.Iconize();
            await icon.SaveAsync(memoryStream, PngFormat.Instance);
            var bytes = memoryStream.ToArray();
            return (IIcon)new Icon(ByteSource.FromStreamFactory(() => new MemoryStream(bytes)), icon.Width);
        });
    }

    public IObservable<byte[]> Bytes => source.Bytes;

    public IDisposable Subscribe(IObserver<byte[]> observer)
    {
        return source.Subscribe(observer);
    }
    
    public int Size { get; }

    public static async Task<Result<IIcon>> FromByteSource(IByteSource data)
    {
        var chunks = await data.Bytes.ToList();
        var bytes = chunks.SelectMany(x => x).ToArray();
        return await FromImage(Image.Load(bytes));
    }
}
