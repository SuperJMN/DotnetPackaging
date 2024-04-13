namespace DotnetPackaging.Deb.Archives.Deb;

public record Metadata
{
    public required string Maintainer { get; init; }
    public required string PackageName { get; init; }
    public required string ApplicationName { get; init; }
    public required string Architecture { get; init; }
    public required string Homepage { get; init; }
    public required string License { get; init; }
    public required string Description { get; init; }
    public required string Version { get; init; }
}