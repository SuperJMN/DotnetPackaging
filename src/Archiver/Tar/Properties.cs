using CSharpFunctionalExtensions;

namespace Archiver.Tar;

public class Properties
{
    public required DateTimeOffset LastModification { get; init; }
    public required FileModes FileModes { get; init; }
    public required Maybe<string> OwnerUsername { get; init; }
    public required Maybe<string> GroupName { get; init; }
    public required Maybe<int> OwnerId { get; init; }
    public required Maybe<int> GroupId { get; init; }
}