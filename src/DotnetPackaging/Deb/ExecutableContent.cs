using CSharpFunctionalExtensions;
using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

public class ExecutableContent : Content
{
    public ExecutableContent(ZafiroPath path, Func<IObservable<byte>> bytes) : base(path, bytes)
    {
    }

    public required Maybe<DesktopEntry> DesktopEntry { get; init; }
}