using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Publish;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Rpm;

public static class RpmPackagerExtensions
{
    public static IByteSource FromProject(
        this RpmPackager packager,
        string projectPath,
        Action<FromDirectoryOptions>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        if (packager == null)
        {
            throw new ArgumentNullException(nameof(packager));
        }

        var options = new FromDirectoryOptions();
        configure?.Invoke(options);

        var publishRequest = new ProjectPublishRequest(projectPath);
        publishConfigure?.Invoke(publishRequest);

        var log = logger ?? Log.Logger;
        var publisher = new DotnetPublisher(Maybe<ILogger>.From(logger));
        var projectFile = new FileInfo(projectPath);

        return ByteSource.FromDisposableAsync(
            () => publisher.Publish(publishRequest),
            container =>
            {
                var resolved = ProjectMetadataDefaults.ResolveFromProject(options, projectFile, log);
                return packager.Pack(container, resolved, log);
            });
    }

    public static async Task<Result> PackProject(
        this RpmPackager packager,
        string projectPath,
        string outputPath,
        Action<FromDirectoryOptions>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        var log = logger ?? Log.Logger;
        
        try
        {
            log.Information("Starting RPM packaging for project: {ProjectPath}", projectPath);
            
            // Configure options
            var options = new FromDirectoryOptions();
            configure?.Invoke(options);
            
            // Configure publish request
            var publishRequest = new ProjectPublishRequest(projectPath);
            publishConfigure?.Invoke(publishRequest);
            
            // Publish the project
            var publisher = new DotnetPublisher(Maybe<ILogger>.From(logger));
            var publishResult = await publisher.Publish(publishRequest);
            
            if (publishResult.IsFailure)
            {
                log.Error("Failed to publish project: {Error}", publishResult.Error);
                return Result.Failure($"Project publish failed: {publishResult.Error}");
            }
            
            log.Information("Project published successfully, creating RPM package...");
            
            // Create the RPM package
            using var container = publishResult.Value;
            var projectFile = new FileInfo(projectPath);
            var resolved = ProjectMetadataDefaults.ResolveFromProject(options, projectFile, log);
            
            var packResult = await packager.Pack(container, resolved, log);
            if (packResult.IsFailure)
            {
                log.Error("Failed to create RPM package: {Error}", packResult.Error);
                return Result.Failure($"RPM package creation failed: {packResult.Error}");
            }
            
            log.Information("RPM package created, writing to: {OutputPath}", outputPath);
            
            // Write the package to disk
            var writeResult = await packResult.Value.WriteTo(outputPath);
            if (writeResult.IsFailure)
            {
                log.Error("Failed to write RPM package to disk: {Error}", writeResult.Error);
                return Result.Failure($"Failed to write RPM package: {writeResult.Error}");
            }
            
            log.Information("RPM package successfully created at: {OutputPath}", outputPath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Unexpected error during RPM packaging");
            return Result.Failure($"Unexpected error: {ex.Message}");
        }
    }
}
