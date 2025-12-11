using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Publish;
using Zafiro.DivineBytes;
using System.IO;
using System.Diagnostics;
using Serilog;
using Serilog.Context;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DotnetPackaging.Tool.Commands;

public static class ExecutionWrapper
{
    public static async Task ExecuteWithLogging(string commandName, string target, Func<ILogger, Task> action)
    {
        using var scope = LogContext.PushProperty("Command", commandName);
        var stopwatch = Stopwatch.StartNew();
        Log.Information("{Command} started for {Target}", commandName, target);
        var logger = Log.ForContext("Command", commandName).ForContext("Target", target);
        try
        {
            await action(logger);
            Log.Information("{Command} completed for {Target} in {Elapsed}", commandName, target, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{Command} failed for {Target}", commandName, target);
            throw;
        }
    }
    
    // Actually, I will implement a cleaner version directly in the code block below.
    public static async Task ExecuteWithPublishedProject(
        string commandName,
        string outputFile,
        FileInfo projectFile,
        string? architecture,
        Func<string?, string, Result<string>> ridResolver,
        bool selfContained,
        string configuration,
        bool singleFile,
        bool trimmed,
        Func<IDisposableContainer, ILogger, Task<Result<PackagingArtifacts>>> packageAction)
    {
         await ExecuteWithLogging(commandName, outputFile, async logger =>
         {
             var ridResult = ridResolver(architecture, commandName);
             if (ridResult.IsFailure)
             {
                 logger.Error("Invalid architecture: {Error}", ridResult.Error);
                 Environment.ExitCode = 1;
                 return;
             }

             var publisher = new DotnetPackaging.Publish.DotnetPublisher(Maybe<ILogger>.From(logger));
             var req = new DotnetPackaging.Publish.ProjectPublishRequest(projectFile.FullName)
             {
                 Rid = Maybe<string>.From(ridResult.Value),
                 SelfContained = selfContained,
                 Configuration = configuration,
                 SingleFile = singleFile,
                 Trimmed = trimmed
             };

             var pubResult = await publisher.Publish(req);
             if (pubResult.IsFailure)
             {
                 logger.Error("Publish failed: {Error}", pubResult.Error);
                 Environment.ExitCode = 1;
                 return;
             }

             var artifactsResult = await packageAction(pubResult.Value, logger);
             if (artifactsResult.IsFailure)
             {
                 logger.Error("Packaging failed: {Error}", artifactsResult.Error);
                 pubResult.Value.Dispose();
                 Environment.ExitCode = 1;
                 return;
             }

             var composite = new CompositeDisposable { pubResult.Value };
             foreach (var disposable in artifactsResult.Value.Disposables)
             {
                 composite.Add(disposable);
             }

             using var session = new PackagingSession(artifactsResult.Value.Packages, composite);
             var package = await session.Packages
                 .Take(1)
                 .DefaultIfEmpty(Result.Failure<INamedByteSource>("No packages were produced"))
                 .FirstAsync();

             if (package.IsFailure)
             {
                 logger.Error("Packaging failed: {Error}", package.Error);
                 Environment.ExitCode = 1;
                 return;
             }

             var writeResult = await package.Value.WriteTo(outputFile);
             if (writeResult.IsFailure)
             {
                 logger.Error("Failed to persist package: {Error}", writeResult.Error);
                 Environment.ExitCode = 1;
                 return;
             }

             logger.Information("{OutputFile}", outputFile);
         });
    }
}
