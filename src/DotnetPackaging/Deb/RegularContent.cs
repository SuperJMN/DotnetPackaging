using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

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

public class RegularContent : Content
{
    public RegularContent(ZafiroPath path, Func<IObservable<byte>> bytes) : base(path, bytes)
    {
    }
}

public class ExecutableContent : Content
{
    public string StartupWMClass;

    public ExecutableContent(ZafiroPath path, Func<IObservable<byte>> bytes, IconResources resources) : base(path, bytes)
    {
        Resources = resources;
    }

    public IconResources Resources { get; }
    public string Name { get; set; }
}