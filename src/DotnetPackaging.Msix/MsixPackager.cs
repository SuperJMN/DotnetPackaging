using CSharpFunctionalExtensions;
using DotnetPackaging.Msix.Core.Manifest;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Msix;

/// <summary>
/// MSIX packager.
/// </summary>
public sealed class MsixPackager
{
    /// <summary>
    /// Creates an MSIX package from a container and optional manifest metadata.
    /// </summary>
    public Task<Result<IByteSource>> Pack(IContainer container, Maybe<AppManifestMetadata> metadata, ILogger? logger = null)
    {
        if (container == null)
        {
            throw new ArgumentNullException(nameof(container));
        }

        var log = Maybe<ILogger>.From(logger);
        var result = metadata.HasValue
            ? Msix.FromDirectoryAndMetadata(container, metadata.Value, log)
            : Msix.FromDirectory(container, log);

        return Task.FromResult(result);
    }
}
