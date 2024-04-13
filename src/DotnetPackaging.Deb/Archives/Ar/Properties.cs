using CSharpFunctionalExtensions;

namespace DotnetPackaging.Deb.Archives.Ar;

public class Properties
{
    public required DateTimeOffset LastModification { get; init; }
    public required LinuxFileMode FileMode { get; init; }
    public required Maybe<int> OwnerId { get; init; }
    public required Maybe<int> GroupId { get; init; }
}