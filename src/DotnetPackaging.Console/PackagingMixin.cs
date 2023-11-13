using System.Text.Json;
using DotnetPackaging.Console.Dtos;

namespace DotnetPackaging.Console;

public static class PackagingMixin
{
    public static async Task<PackagingDto> ToPackaging(this FileInfo metadataFile)
    {
        await using var fileStream = metadataFile.OpenRead();
        var data = await JsonSerializer.DeserializeAsync<PackagingDto>(fileStream);
        return data!;
    }
}