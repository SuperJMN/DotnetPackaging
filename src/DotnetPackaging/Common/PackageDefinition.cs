using DotnetPackaging.Archives.Deb;
using Zafiro.FileSystem;

namespace DotnetPackaging.Common;

public record PackageDefinition(Metadata Metadata, Dictionary<ZafiroPath, ExecutableMetadata> ExecutableMappings);