using DotnetPackaging.Common;
using Zafiro.FileSystem;

namespace DotnetPackaging.New.Deb.Contents;

public abstract class Content : IByteFlow
{
    private readonly IByteFlow byteFlow;

    public Content(ZafiroPath path, IByteFlow byteFlow)
    {
        this.byteFlow = byteFlow;
        Path = path;
    }

    public ZafiroPath Path { get; }
    public IObservable<byte> Bytes => byteFlow.Bytes;
    public long Length => byteFlow.Length;
}