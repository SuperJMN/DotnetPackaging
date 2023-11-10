using CSharpFunctionalExtensions;
using DotnetPackaging.Common;
using DotnetPackaging.Old.Deb;
using Zafiro.FileSystem;

namespace DotnetPackaging.New.Deb.Contents;

public class ExecutableContent : Content
{
    public ExecutableContent(ZafiroPath path, ByteFlow bytes) : base(path, bytes)
    {
    }

    public required Maybe<DesktopEntry> DesktopEntry { get; init; }
    public required string CommandName { get; init; }
}