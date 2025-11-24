using CSharpFunctionalExtensions;

namespace DotnetPackaging.Deb.Unix;

public record UnixFileProperties
{
    public required PosixFileMode FileMode { get; init; }
    public required Maybe<int> OwnerId { get; init; }
    public required Maybe<int> GroupId { get; init; }
    public required Maybe<string> OwnerUsername { get; init; }
    public required Maybe<string> GroupName { get; init; }
    public required DateTimeOffset LastModification { get; init; }
}
