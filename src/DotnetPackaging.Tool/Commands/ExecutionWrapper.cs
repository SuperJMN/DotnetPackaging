using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Publish;
using Zafiro.DivineBytes;
using System.IO;
using System.Diagnostics;
using Serilog;
using Serilog.Context;

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
        Func<IDisposableContainer, ILogger, Result<IByteSource>> packager)
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

            var publisher = new DotnetPublisher(Maybe<ILogger>.From(logger));
            var req = new ProjectPublishRequest(projectFile.FullName)
            {
                Rid = Maybe<string>.From(ridResult.Value),
                SelfContained = selfContained,
                Configuration = configuration,
                SingleFile = singleFile,
                Trimmed = trimmed
            };

            var lazyPackage = ByteSource.FromDisposableAsync(
                () => publisher.Publish(req),
                container => packager(container, logger));

            var writeResult = await lazyPackage.WriteTo(outputFile);
            if (writeResult.IsFailure)
            {
                logger.Error("Failed: {Error}", writeResult.Error);
                Environment.ExitCode = 1;
                return;
            }

            logger.Information("{OutputFile}", outputFile);
        });
    }

    public static async Task ExecuteWithPublishedProjectAsync(
        string commandName,
        string outputFile,
        FileInfo projectFile,
        string? architecture,
        Func<string?, string, Result<string>> ridResolver,
        bool selfContained,
        string configuration,
        bool singleFile,
        bool trimmed,
        Func<IDisposableContainer, ILogger, Task<Result<IByteSource>>> packager)
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

            var publisher = new DotnetPublisher(Maybe<ILogger>.From(logger));
            var req = new ProjectPublishRequest(projectFile.FullName)
            {
                Rid = Maybe<string>.From(ridResult.Value),
                SelfContained = selfContained,
                Configuration = configuration,
                SingleFile = singleFile,
                Trimmed = trimmed
            };

            var lazyPackage = ByteSource.FromDisposableAsync(
                () => publisher.Publish(req),
                container => packager(container, logger));

            var writeResult = await lazyPackage.WriteTo(outputFile);
            if (writeResult.IsFailure)
            {
                logger.Error("Failed: {Error}", writeResult.Error);
                Environment.ExitCode = 1;
                return;
            }

            logger.Information("{OutputFile}", outputFile);
        });
    }
}
