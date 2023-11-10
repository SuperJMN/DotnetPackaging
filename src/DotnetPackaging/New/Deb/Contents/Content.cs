using DotnetPackaging.Common;
using Zafiro.FileSystem;

namespace DotnetPackaging.New.Deb.Contents;

public abstract class Content : IByteFlow
{
    private readonly ByteFlow byteFlow;

    public Content(ZafiroPath path, ByteFlow byteFlow)
    {
        this.byteFlow = byteFlow;
        Path = path;
    }

    public ZafiroPath Path { get; }
    public IObservable<byte> Bytes => byteFlow.Bytes;
    public long Length => byteFlow.Length;
}