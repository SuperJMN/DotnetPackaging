using System.Text.Json;
using DotnetPackaging.Client.Dtos;

namespace DotnetPackaging.Common;

public static class PackageDefinitionMixin
{
    public static async Task<PackageDefinition> ToPackageDefinition(this FileInfo metadataFile)
    {
        var packageDefinitionDto = await metadataFile.ToDto();
        return packageDefinitionDto.ToModel();
    }

    public static async Task<PackageDefinitionDto> ToDto(this FileInfo metadataFile)
    {
        await using var fileStream = metadataFile.OpenRead();
        var data = await JsonSerializer.DeserializeAsync<PackageDefinitionDto>(fileStream);
        return data!;
    }
}