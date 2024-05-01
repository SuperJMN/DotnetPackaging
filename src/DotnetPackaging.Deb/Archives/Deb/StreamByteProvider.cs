using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Deb;

public class StreamByteProvider : IByteProvider
{
    private readonly IByteProvider byteProvider;

    public StreamByteProvider(Func<Stream> stream)
    {
    }
    
    public IObservable<byte[]> Bytes => byteProvider.Bytes;
    public long Length => byteProvider.Length;
}