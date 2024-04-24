using CSharpFunctionalExtensions;
using Zafiro.FileSystem;

namespace DotnetPackaging.Deb.Archives.Deb.Contents;

public class ExecutableContent : Content
{
    public ExecutableContent(ZafiroPath path, ByteFlow bytes) : base(path, bytes)
    {
    }

    public required Maybe<DesktopEntry> DesktopEntry { get; init; }
    public required string CommandName { get; init; }
}