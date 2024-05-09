using CSharpFunctionalExtensions;

namespace DotnetPackaging.Deb.Archives.Ar;

public record Properties
{
    public required DateTimeOffset LastModification { get; init; }
    public required UnixFileMode FileMode { get; init; }
    public required Maybe<int> OwnerId { get; init; }
    public required Maybe<int> GroupId { get; init; }
}