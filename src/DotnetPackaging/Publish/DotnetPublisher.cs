using Zafiro.Commands;
using Zafiro.Mixins;
using Result = CSharpFunctionalExtensions.Result;

namespace DotnetPackaging.Publish;

public sealed class DotnetPublisher : IPublisher
{
    private readonly ICommand command;
    private readonly Maybe<ILogger> logger;

    public DotnetPublisher() : this(Maybe<ILogger>.From(Log.Logger))
    {
    }

    public DotnetPublisher(Maybe<ILogger> logger) : this(new Command(logger.Map(x => x.ForContext("Module", "COMMAND"))), logger)
    {
    }

    public DotnetPublisher(ICommand command, Maybe<ILogger> logger)
    {
        this.command = command;
        this.logger = logger;
    }

    public async Task<Result<IDisposableContainer>> Publish(ProjectPublishRequest request)
    {
        try
        {
            logger.Information("Preparing to publish project {ProjectPath}", request.ProjectPath);

            // Validate that RID is provided when SelfContained is true
            if (request.SelfContained && !request.Rid.HasValue)
            {
                var error = $"RID is required when publishing self-contained applications. Please specify a RID for project {request.ProjectPath}";
                logger.Error(error);
                return Result.Failure<IDisposableContainer>(error);
            }

            var outputDirResult = PrepareOutputDirectory();
            if (outputDirResult.IsFailure)
            {
                return Result.Failure<IDisposableContainer>(outputDirResult.Error);
            }

            var outputDir = outputDirResult.Value;
            var args = BuildArgs(request, outputDir);
            LogPublishConfiguration(request);

            var run = await command.Execute("dotnet", args);
            if (run.IsFailure)
            {
                logger.Error("dotnet publish failed for {ProjectPath}: {Error}", request.ProjectPath, run.Error);
                return Result.Failure<IDisposableContainer>(run.Error);
            }

            logger.Information("dotnet publish completed for {ProjectPath}", request.ProjectPath);

            var name = await DeriveName(request.ProjectPath);
            logger.Information("Publish succeeded for {ProjectPath} (Name: {Name})", request.ProjectPath, name.GetValueOrDefault("unknown"));

            return Result.Success<IDisposableContainer>(new DisposableDirectoryContainer(outputDir, logger.GetValueOrDefault(Log.Logger)));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Unexpected failure during publish for {ProjectPath}", request.ProjectPath);
            return Result.Failure<IDisposableContainer>($"Unexpected error during publish: {ex.Message}");
        }
    }

    private async Task<Maybe<string>> DeriveName(string projectPath)
    {
        try
        {
            // Try to get AssemblyName from project properties
            var assemblyNameResult = await command.Execute("dotnet", $"msbuild \"{projectPath}\" -getProperty:AssemblyName");
            if (assemblyNameResult.IsSuccess)
            {
                var output = string.Join("", assemblyNameResult.Value).Trim();
                if (!string.IsNullOrWhiteSpace(output))
                {
                    logger.Debug("Detected AssemblyName: {AssemblyName}", output);
                    return Maybe<string>.From(output);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Execute(l => l.Warning(ex, "Failed to retrieve AssemblyName property"));
        }

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
            logger.Debug("Using temporary publish directory {Directory}", outputDir);
            return outputDir;
        }, ex =>
        {
            logger.Error(ex, "Unable to create temporary publish directory");
            return $"Unable to create publish directory: {ex.Message}";
        });
    }

    private void LogPublishConfiguration(ProjectPublishRequest request)
    {
        var ridDisplay = request.Rid.Match(
            value => value,
            () => "(default)");
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

        if (r.Rid.HasValue)
        {
            sb.Append($"-r {r.Rid.Value} ");
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

}
