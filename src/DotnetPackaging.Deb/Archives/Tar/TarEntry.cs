using CSharpFunctionalExtensions;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.Deb.Archives.Tar;

public abstract record TarEntry(string Path, TarEntryProperties Properties);

public record TarEntryProperties
{
    public required UnixPermissions Permissions { get; init; }
    public required Maybe<int> OwnerId { get; init; }
    public required Maybe<int> GroupId { get; init; }
    public required Maybe<string> OwnerUsername { get; init; }
    public required Maybe<string> GroupName { get; init; }
    public required DateTimeOffset LastModification { get; init; }
}
