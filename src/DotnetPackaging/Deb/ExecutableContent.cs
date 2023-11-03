using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

public class ExecutableContent : Content
{
    public ExecutableContent(ZafiroPath path, Func<IObservable<byte>> bytes, IconResources icons) : base(path, bytes)
    {
        Icons = icons;
    }

    public IconResources Icons { get; }
    public required string Name { get; init; }
    public required string StartupWmClass { get; set; }
}