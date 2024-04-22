using CSharpFunctionalExtensions;

namespace DotnetPackaging.Deb.Archives.Tar;

public record TarProperties
{
    public required DateTimeOffset LastModification { get; init; }
    public required UnixFilePermissions FileMode { get; init; }
    public required Maybe<string> OwnerUsername { get; init; }
    public required Maybe<string> GroupName { get; init; }
    public required Maybe<int> OwnerId { get; init; }
    public required Maybe<int> GroupId { get; init; }
    public required int LinkIndicator { get; init; }
}