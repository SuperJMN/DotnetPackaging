namespace DotnetPackaging.Deb.Archives.Tar;

public record UnixFileProperties
{
    public required int Mode { get; init; }
    public required int OwnerId { get; init; }
    public required int GroupId { get; init; }
    public string OwnerUsername { get; init; } = "root";
    public string GroupName { get; init; } = "root";
    public DateTimeOffset LastModification { get; init; }
}
