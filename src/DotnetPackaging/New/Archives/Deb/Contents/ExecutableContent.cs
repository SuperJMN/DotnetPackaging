using CSharpFunctionalExtensions;
using DotnetPackaging.Common;
using Zafiro.FileSystem;

namespace DotnetPackaging.New.Archives.Deb.Contents;

public class ExecutableContent : Content
{
    public ExecutableContent(ZafiroPath path, ByteFlow bytes) : base(path, bytes)
    {
    }

    public required Maybe<DesktopEntry> DesktopEntry { get; init; }
    public required string CommandName { get; init; }
}