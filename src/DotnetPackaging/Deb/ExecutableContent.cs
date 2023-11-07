using CSharpFunctionalExtensions;
using DotnetPackaging.Common;
using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

public class ExecutableContent : Content
{
    public ExecutableContent(ZafiroPath path, IByteStore byteStore) : base(path, byteStore)
    {
    }

    public required Maybe<DesktopEntry> DesktopEntry { get; init; }
    public required string CommandName { get; init; }
}