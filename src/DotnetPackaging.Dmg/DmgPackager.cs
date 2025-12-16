using CSharpFunctionalExtensions;
using DotnetPackaging.Publish;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Dmg;

/// <summary>
/// High-level API for creating DMG disk images from .NET projects.
/// </summary>
public static class DmgPackager
{
    /// <summary>
    /// Creates a lazy IByteSource that publishes the project and packages it as a DMG on-demand.
    /// </summary>
    public static IByteSource FromProject(
        string projectPath,
        Action<DmgOptions>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        var options = new DmgOptions();
        configure?.Invoke(options);

        var publishRequest = new ProjectPublishRequest(projectPath);
        publishConfigure?.Invoke(publishRequest);

        var publisher = new DotnetPublisher(Maybe<ILogger>.From(logger));
        var projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath);

        return ByteSource.FromDisposableAsync(
            () => publisher.Publish(publishRequest),
            async container =>
            {
                var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
                var dmgPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dp-dmg-{System.Guid.NewGuid():N}.dmg");
                try
                {
                    var writeResult = await container.WriteTo(tempDir);
                    if (writeResult.IsFailure)
                    {
                        return Result.Failure<IByteSource>(writeResult.Error);
                    }

                    var volName = options.VolumeName ?? projectName;
                    await DmgHfsBuilder.Create(
                        tempDir,
                        dmgPath,
                        volName,
                        options.Compress,
                        options.AddApplicationsSymlink,
                        options.IncludeDefaultLayout,
                        options.Icon,
                        options.ExecutableName ?? projectName);

                    return Result.Success<IByteSource>(ByteSource.FromStreamFactory(() => System.IO.File.OpenRead(dmgPath)));
                }
                finally
                {
                    if (System.IO.Directory.Exists(tempDir))
                    {
                        System.IO.Directory.Delete(tempDir, true);
                    }
                }
            });
    }

    /// <summary>
    /// Publishes the project, packages it as a DMG, and writes to the output path.
    /// </summary>
    public static async Task<Result> PackProject(
        string projectPath,
        string outputPath,
        Action<DmgOptions>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        var source = FromProject(projectPath, configure, publishConfigure, logger);
        return await source.WriteTo(outputPath);
    }
}

/// <summary>
/// Options for DMG packaging.
/// </summary>
public class DmgOptions
{
    public string? VolumeName { get; set; }
    public string? ExecutableName { get; set; }
    public bool Compress { get; set; } = true;
    public bool AddApplicationsSymlink { get; set; } = true;
    public bool IncludeDefaultLayout { get; set; } = true;
    public Maybe<IIcon> Icon { get; set; } = Maybe<IIcon>.None;
}
