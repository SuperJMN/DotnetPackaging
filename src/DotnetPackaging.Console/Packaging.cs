﻿using System.Text.Json;
using DotnetPackage.Console.Dtos;
using DotnetPackaging.Old.Deb;
using Zafiro.FileSystem;

namespace DotnetPackage.Console;

public class Packaging
{
    public Packaging(Metadata metadata, Dictionary<ZafiroPath, ExecutableMetadata> executableMappings)
    {
        Metadata = metadata;
        ExecutableMappings = executableMappings;
    }

    public Metadata Metadata { get; }
    public Dictionary<ZafiroPath, ExecutableMetadata> ExecutableMappings { get; }

    public static async Task<PackagingDto> FromFile(FileInfo metadataFile)
    {
        await using var fileStream = metadataFile.OpenRead();
        var options = new JsonSerializerOptions();
        var data = await JsonSerializer.DeserializeAsync<PackagingDto>(fileStream, options);
        return data!;
    }
}