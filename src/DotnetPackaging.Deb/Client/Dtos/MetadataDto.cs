namespace DotnetPackaging.Deb.Client.Dtos;

public class MetadataDto
{
    public required string Maintainer { get; set; }
    public required string PackageName { get; set; }
    public required string ApplicationName { get; set; }
    public required string Architecture { get; set; }
    public required string Homepage { get; set; }
    public required string License { get; set; }
    public required string Description { get; set; }
    public required string Version { get; set; }
}