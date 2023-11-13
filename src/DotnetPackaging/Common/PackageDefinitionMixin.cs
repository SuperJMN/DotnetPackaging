using System.Text.Json;
using DotnetPackaging.Client.Dtos;

namespace DotnetPackaging.Common;

public static class PackageDefinitionMixin
{
    public static async Task<PackageDefinition> ToPackageDefinition(this FileInfo metadataFile)
    {
        var packageDefinitionDto = await metadataFile.ToDto();
        return await packageDefinitionDto.ToModel();
    }

    public static async Task<PackageDefinitionDto> ToDto(this FileInfo metadataFile)
    {
        await using var fileStream = metadataFile.OpenRead();
        var data = await JsonSerializer.DeserializeAsync<PackageDefinitionDto>(fileStream);

        var packageDefinitionDto = data! with
        {
            Executables = new Dictionary<string, ExecutableMetadataDto>(data.Executables.Select(pair =>
            {
                return new KeyValuePair<string, ExecutableMetadataDto>(pair.Key, pair.Value with
                {
                    DesktopEntry = pair.Value.DesktopEntry with
                    {
                        Icons = new Dictionary<int, string>(pair.Value.DesktopEntry.Icons.Select(valuePair =>
                            new KeyValuePair<int, string>(valuePair.Key,
                                Path.Combine(metadataFile.DirectoryName!, valuePair.Value))))
                    }
                });
            }))
        };

        return packageDefinitionDto;
    }
}