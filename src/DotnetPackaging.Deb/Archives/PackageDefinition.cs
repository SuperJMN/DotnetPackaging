using DotnetPackaging.Deb.Archives.Deb;
using Zafiro.FileSystem;

namespace DotnetPackaging.Deb.Archives;

public record PackageDefinition(Metadata Metadata, Dictionary<ZafiroPath, ExecutableMetadata> ExecutableMappings);