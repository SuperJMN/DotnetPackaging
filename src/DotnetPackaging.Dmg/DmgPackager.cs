using CSharpFunctionalExtensions;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Dmg;

/// <summary>
/// DMG packager.
/// </summary>
public sealed class DmgPackager
{
    /// <summary>
    /// Creates a DMG from a container and metadata.
    /// </summary>
    public async Task<Result<IByteSource>> Pack(IContainer container, DmgPackagerMetadata metadata, ILogger? logger = null)
    {
        if (container == null)
        {
            throw new ArgumentNullException(nameof(container));
        }

        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        var dmgPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dp-dmg-{System.Guid.NewGuid():N}.dmg");
        try
        {
            var writeResult = await container.WriteTo(tempDir);
            if (writeResult.IsFailure)
            {
                return Result.Failure<IByteSource>(writeResult.Error);
            }

            var volumeName = metadata.VolumeName
                .Or(metadata.ExecutableName)
                .GetValueOrDefault("Application");

            await DmgHfsBuilder.Create(
                tempDir,
                dmgPath,
                volumeName,
                metadata.Compress.GetValueOrDefault(true),
                metadata.AddApplicationsSymlink.GetValueOrDefault(true),
                metadata.IncludeDefaultLayout.GetValueOrDefault(true),
                metadata.Icon,
                metadata.ExecutableName.GetValueOrDefault(null));

            return Result.Success<IByteSource>(ByteSource.FromStreamFactory(() => System.IO.File.OpenRead(dmgPath)));
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }
    }
}

/// <summary>
/// Options for DMG packaging.
/// </summary>
internal class DmgOptions
{
    public string? VolumeName { get; set; }
    public string? ExecutableName { get; set; }
    public bool Compress { get; set; } = true;
    public bool AddApplicationsSymlink { get; set; } = true;
    public bool IncludeDefaultLayout { get; set; } = true;
    public Maybe<IIcon> Icon { get; set; } = Maybe<IIcon>.None;
}
