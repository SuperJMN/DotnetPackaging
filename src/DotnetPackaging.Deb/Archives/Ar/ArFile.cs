using CSharpFunctionalExtensions;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.Deb.Archives.Ar;

public record ArFile(params ArEntry[] Entries);

public record ArEntry(string Name, byte[] Content, ArEntryProperties Properties);

public record ArEntryProperties
{
    public required DateTimeOffset LastModification { get; init; }
    public required UnixPermissions Permissions { get; init; }
    public required Maybe<int> OwnerId { get; init; }
    public required Maybe<int> GroupId { get; init; }
}
