using System;
using System.IO;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Zafiro.Commands;
using Zafiro.DivineBytes;
using Zafiro.FileSystem.Core;
using Zafiro.Mixins;
using LocalDirectory = Zafiro.FileSystem.Local.Directory;

namespace DotnetPackaging.Publish;

public sealed class DotnetPublisher : IPublisher
{
    private readonly ICommand command;
    private readonly Maybe<ILogger> logger;

    public DotnetPublisher() : this(Maybe<ILogger>.From(Log.Logger))
    {
    }

    public DotnetPublisher(Maybe<ILogger> logger) : this(new Command(logger), logger)
    {
    }

    public DotnetPublisher(ICommand command, Maybe<ILogger> logger)
    {
        this.command = command;
        this.logger = logger;
    }

    public async Task<Result<PublishResult>> Publish(ProjectPublishRequest request)
    {
        try
        {
            logger.Information("Preparing to publish project {ProjectPath}", request.ProjectPath);

            var outputDirResult = PrepareOutputDirectory();
            if (outputDirResult.IsFailure)
            {
                return Result.Failure<PublishResult>(outputDirResult.Error);
            }

            var outputDir = outputDirResult.Value;
            var args = BuildArgs(request, outputDir);
            LogPublishConfiguration(request);

            var run = await command.Execute("dotnet", args);
            if (run.IsFailure)
            {
                logger.Error("dotnet publish failed for {ProjectPath}: {Error}", request.ProjectPath, run.Error);
                return Result.Failure<PublishResult>(run.Error);
            }

            logger.Information("dotnet publish completed for {ProjectPath}", request.ProjectPath);

            var fileSystem = new FileSystem();
            var localDir = new LocalDirectory(fileSystem.DirectoryInfo.New(outputDir));
            var readOnly = await localDir.ToDirectory();
            if (readOnly.IsFailure)
            {
                logger.Error("Unable to materialize directory {Directory}: {Error}", outputDir, readOnly.Error);
                return Result.Failure<PublishResult>($"Unable to materialize directory: {readOnly.Error}");
            }

            var containerResult = ContainerUtils.BuildContainer(readOnly.Value);
            if (containerResult.IsFailure)
            {
                logger.Error("Failed to build container for {Directory}: {Error}", outputDir, containerResult.Error);
                return Result.Failure<PublishResult>(containerResult.Error);
            }

            var name = DeriveName(request.ProjectPath);
            logger.Information("Publish succeeded for {ProjectPath} (Name: {Name})", request.ProjectPath, name.GetValueOrDefault("unknown"));

            return Result.Success(new PublishResult(containerResult.Value, name, outputDir));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Unexpected failure during publish for {ProjectPath}", request.ProjectPath);
            return Result.Failure<PublishResult>($"Unexpected error during publish: {ex.Message}");
        }
    }

    private static Maybe<string> DeriveName(string projectPath)
    {
        try
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(projectPath);
            return string.IsNullOrWhiteSpace(fileName) ? Maybe<string>.None : Maybe<string>.From(fileName);
        }
        catch
        {
            return Maybe<string>.None;
        }
    }

    private Result<string> PrepareOutputDirectory()
    {
        return Result.Try(() =>
        {
            var outputDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dp-publish-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(outputDir);
            logger.Information("Using temporary publish directory {Directory}", outputDir);
            return outputDir;
        }, ex =>
        {
            logger.Error(ex, "Unable to create temporary publish directory");
            return $"Unable to create publish directory: {ex.Message}";
        });
    }

    private void LogPublishConfiguration(ProjectPublishRequest request)
    {
        var rid = request.Rid.Match(
            value => value,
            () => request.SelfContained ? InferHostRid() : null);
        var ridDisplay = rid ?? "(default)";
        logger.Information(
            "Executing dotnet publish for {ProjectPath} | Configuration: {Configuration} | Self-contained: {SelfContained} | Single-file: {SingleFile} | Trimmed: {Trimmed} | RID: {Rid}",
            request.ProjectPath,
            request.Configuration,
            request.SelfContained,
            request.SingleFile,
            request.Trimmed,
            ridDisplay);
    }

    private static string BuildArgs(ProjectPublishRequest r, string outputDir)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"publish \"{r.ProjectPath}\" ");
        sb.Append($"-c {r.Configuration} ");

        string? ridToUse = r.Rid.HasValue
            ? r.Rid.Value
            : (r.SelfContained ? InferHostRid() : null);
        if (!string.IsNullOrWhiteSpace(ridToUse))
        {
            sb.Append($"-r {ridToUse} ");
        }

        sb.Append(r.SelfContained ? "--self-contained true " : "--self-contained false ");
        if (r.SingleFile) sb.Append("/p:PublishSingleFile=true ");
        if (r.Trimmed) sb.Append("/p:PublishTrimmed=true ");
        if (r.MsBuildProperties is not null)
        {
            foreach (var kv in r.MsBuildProperties)
            {
                var val = kv.Value.Replace("\"", "\\\"");
                sb.Append($" /p:{kv.Key}=\"{val}\"");
            }
        }
        sb.Append($" -o \"{outputDir}\"");
        return sb.ToString();
    }

    private static string? InferHostRid()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return RuntimeInformation.OSArchitecture switch
                {
                    System.Runtime.InteropServices.Architecture.X64 => "linux-x64",
                    System.Runtime.InteropServices.Architecture.Arm64 => "linux-arm64",
                    System.Runtime.InteropServices.Architecture.X86 => "linux-x86",
                    System.Runtime.InteropServices.Architecture.Arm => "linux-arm",
                    _ => null
                };
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return RuntimeInformation.OSArchitecture switch
                {
                    System.Runtime.InteropServices.Architecture.X64 => "win-x64",
                    System.Runtime.InteropServices.Architecture.Arm64 => "win-arm64",
                    System.Runtime.InteropServices.Architecture.X86 => "win-x86",
                    System.Runtime.InteropServices.Architecture.Arm => "win-arm",
                    _ => null
                };
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.OSArchitecture switch
                {
                    System.Runtime.InteropServices.Architecture.X64 => "osx-x64",
                    System.Runtime.InteropServices.Architecture.Arm64 => "osx-arm64",
                    _ => null
                };
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
