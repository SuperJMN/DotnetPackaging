using Zafiro.DataModel;
using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Core;

public class UriRuntime : IRuntime
{
    private readonly IByteSource source;

    private UriRuntime(IByteSource source)
    {
        this.source = source;
    }

    public static async Task<Result<UriRuntime>> Create(Uri uri)
    {
        var data = ByteSource.FromStreamFactory(() => await HttpRequestData.Create(uri));
        return data.Map(data => new UriRuntime(data));
    }

    public IObservable<byte[]> Bytes => source.Bytes;

    public long Length => source.Length;
}