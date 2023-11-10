using CSharpFunctionalExtensions;

namespace DotnetPackaging.Old.Tar;

public class Properties
{
    public required DateTimeOffset LastModification { get; init; }
    public required FileMode FileMode { get; init; }
    public required Maybe<string> OwnerUsername { get; init; }
    public required Maybe<string> GroupName { get; init; }
    public required Maybe<int> OwnerId { get; init; }
    public required Maybe<int> GroupId { get; init; }
    public required long Length { get; init; }
    public required int LinkIndicator { get; init; }
}