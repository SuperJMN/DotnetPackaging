namespace Archiver.Deb;

public class Metadata
{
    public required string Maintainer { get; init; }
    public required string PackageName { get; init; }
    public required string ApplicationName { get; init; }
    public required string Architecture { get; init; }
    public required string Homepage { get; init; }
    public required string License { get; init; }
}