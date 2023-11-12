namespace DotnetPackaging.Console.Dtos;

public class PackagingDto
{
    public Dictionary<string, ExecutableMetadataDto> Executables { get; set; }
    public MetadataDto PackageMetadata { get; set; }
}