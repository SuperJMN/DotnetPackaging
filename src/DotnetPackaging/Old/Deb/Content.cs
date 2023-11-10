using Zafiro.FileSystem;

namespace DotnetPackaging.Old.Deb;

public abstract class Content
{
    public Content(ZafiroPath path, Func<IObservable<byte>> bytes)
    {
        Bytes = bytes;
        Path = path;
    }

    public ZafiroPath Path { get; }

    public Func<IObservable<byte>> Bytes { get; }
}