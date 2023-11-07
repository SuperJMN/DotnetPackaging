using System.Text.Json;
using DotnetPackage.Console.Dtos;
using DotnetPackaging.Deb;
using Zafiro.FileSystem;

namespace DotnetPackage.Console;

public class Packaging
{
    public Metadata Metadata { get; }
    public Dictionary<ZafiroPath, ExecutableMetadata> ExecutableMappings { get; }

    public Packaging(Metadata metadata, Dictionary<ZafiroPath, ExecutableMetadata> executableMappings)
    {
        Metadata = metadata;
        ExecutableMappings = executableMappings;
    }

    public static async Task<Packaging> FromFile(FileInfo metadataFile)
    {
        await using var fileStream = metadataFile.OpenRead();
        var options = new JsonSerializerOptions();
        var data = await JsonSerializer.DeserializeAsync<PackagingDto>(fileStream, options);
        return data!.ToModel();
    }
}