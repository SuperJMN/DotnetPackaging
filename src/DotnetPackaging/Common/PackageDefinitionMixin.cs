using System.Text.Json;
using DotnetPackaging.Client.Dtos;

namespace DotnetPackaging.Common;

public static class PackageDefinitionMixin
{
    public static async Task<PackageDefinitionDto> ToPackageDefinition(this FileInfo metadataFile)
    {
        await using var fileStream = metadataFile.OpenRead();
        var data = await JsonSerializer.DeserializeAsync<PackageDefinitionDto>(fileStream);
        return data!;
    }
}