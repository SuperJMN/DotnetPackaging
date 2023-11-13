using DotnetPackaging.Archives.Deb;
using Zafiro.FileSystem;

namespace DotnetPackaging.Common;

public class PackageDefinition
{
    public PackageDefinition(Metadata metadata, Dictionary<ZafiroPath, ExecutableMetadata> executableMappings)
    {
        Metadata = metadata;
        ExecutableMappings = executableMappings;
    }

    public Metadata Metadata { get; }
    public Dictionary<ZafiroPath, ExecutableMetadata> ExecutableMappings { get; }
}