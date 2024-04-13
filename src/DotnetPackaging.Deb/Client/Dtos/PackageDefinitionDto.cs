namespace DotnetPackaging.Deb.Client.Dtos;

public record PackageDefinitionDto(Dictionary<string, ExecutableMetadataDto> Executables, MetadataDto PackageMetadata);