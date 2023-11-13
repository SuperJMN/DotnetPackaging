namespace DotnetPackaging.Client.Dtos;

public class PackageDefinitionDto
{
    public Dictionary<string, ExecutableMetadataDto> Executables { get; set; }
    public MetadataDto PackageMetadata { get; set; }
}