using DotnetPackaging.Common;
using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

public abstract class Content
{
    public Content(ZafiroPath path, IByteStore byteStore)
    {
        Path = path;
        ByteStore = byteStore;
    }

    public ZafiroPath Path { get; }
    public IByteStore ByteStore { get; }
}