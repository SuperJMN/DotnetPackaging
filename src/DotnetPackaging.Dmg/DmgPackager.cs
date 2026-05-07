using CSharpFunctionalExtensions;
using DotnetPackaging.Publish;
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

        if (container is DisposableDirectoryContainer directoryContainer)
        {
            return await PackDirectory(directoryContainer.OutputPath, metadata, logger);
        }

        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var writeResult = await container.WriteTo(tempDir);
            if (writeResult.IsFailure)
            {
                return Result.Failure<IByteSource>(writeResult.Error);
            }

            return await PackDirectory(new DirectoryInfo(tempDir), metadata, logger);
        }
        catch (Exception ex)
        {
            return Result.Failure<IByteSource>(ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Creates a DMG directly from a directory, preserving source filesystem metadata where supported.
    /// </summary>
    public Task<Result<IByteSource>> PackDirectory(string directoryPath, DmgPackagerMetadata metadata, ILogger? logger = null)
    {
        if (directoryPath == null)
        {
            throw new ArgumentNullException(nameof(directoryPath));
        }

        return PackDirectory(new DirectoryInfo(directoryPath), metadata, logger);
    }

    /// <summary>
    /// Creates a DMG directly from a directory, preserving source filesystem metadata where supported.
    /// </summary>
    public async Task<Result<IByteSource>> PackDirectory(DirectoryInfo directory, DmgPackagerMetadata metadata, ILogger? logger = null)
    {
        if (directory == null)
        {
            throw new ArgumentNullException(nameof(directory));
        }

        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        if (!directory.Exists)
        {
            return Result.Failure<IByteSource>($"Directory '{directory.FullName}' does not exist.");
        }

        var dmgPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dp-dmg-{Guid.NewGuid():N}.dmg");
        try
        {
            var volumeName = metadata.VolumeName
                .Or(metadata.ExecutableName)
                .GetValueOrDefault("Application");
            var executableName = metadata.ExecutableName.HasValue ? metadata.ExecutableName.Value : null;

            await DmgHfsBuilder.Create(
                directory.FullName,
                dmgPath,
                volumeName,
                metadata.Compress.GetValueOrDefault(true),
                metadata.AddApplicationsSymlink.GetValueOrDefault(true),
                metadata.IncludeDefaultLayout.GetValueOrDefault(true),
                metadata.Icon,
                executableName,
                metadata.InfoPlist,
                metadata.BundleIdentifier,
                metadata.BundleVersion,
                metadata.Vendor);

            return Result.Success<IByteSource>(TemporaryFileByteSource.OpenReadAndDelete(dmgPath));
        }
        catch (Exception ex)
        {
            TryDeleteFile(dmgPath);
            return Result.Failure<IByteSource>(ex.Message);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
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
    public Maybe<IByteSource> InfoPlist { get; set; } = Maybe<IByteSource>.None;
    public Maybe<string> BundleIdentifier { get; set; } = Maybe<string>.None;
    public Maybe<string> BundleVersion { get; set; } = Maybe<string>.None;
    public Maybe<string> Vendor { get; set; } = Maybe<string>.None;
}
