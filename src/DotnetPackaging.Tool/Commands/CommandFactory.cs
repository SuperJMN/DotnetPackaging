using System.CommandLine;
using Serilog;

namespace DotnetPackaging.Tool.Commands;

public record CommandSet(Command Root, Command FromDirectory);

public static class CommandFactory
{
    public static CommandSet CreateCommand(
        string commandName,
        string friendlyName,
        string extension,
        Func<DirectoryInfo, FileInfo, Options, ILogger, Task> handler,
        string? description = null,
        Option<bool>? defaultLayoutOption = null,
        Action<Options, ParseResult>? optionsPostProcessor = null,
        params string[] aliases)
    {
        var defaultDescription = description ??
                                 $"Create a {friendlyName} from a directory with the published application contents. Everything is inferred. For .NET apps this is usually the 'publish' directory.";

        // --- Base command (deprecated, backward-compatible) ---
        var baseBuildDir = new Option<DirectoryInfo>("--directory")
        {
            Description = "Published application directory (for example: bin/Release/<tfm>/publish)",
            Required = true
        };
        var baseOutput = new Option<FileInfo>("--output")
        {
            Description = $"Destination path for the generated {extension} file",
            Required = true
        };
        var baseMetadata = new MetadataOptionSet();

        var rootCommand = new Command(commandName, defaultDescription);
        foreach (var alias in aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                rootCommand.Aliases.Add(alias);
            }
        }

        rootCommand.Add(baseBuildDir);
        rootCommand.Add(baseOutput);
        baseMetadata.AddTo(rootCommand);
        if (defaultLayoutOption != null)
        {
            rootCommand.Add(defaultLayoutOption);
        }

        var baseBinder = baseMetadata.CreateBinder(defaultLayoutOption);

        rootCommand.SetAction(async parseResult =>
        {
            Console.Error.WriteLine($"Warning: 'dotnetpackager {commandName} --directory' is deprecated and will be removed in a future version. Use 'dotnetpackager {commandName} from-directory' instead.");
            var directory = parseResult.GetValue(baseBuildDir)!;
            var output = parseResult.GetValue(baseOutput)!;
            var opts = baseBinder.Bind(parseResult);
            optionsPostProcessor?.Invoke(opts, parseResult);
            await ExecutionWrapper.ExecuteWithLogging(commandName, output.FullName, logger => handler(directory, output, opts, logger));
        });

        // --- from-directory subcommand (canonical path) ---
        var fdBuildDir = new Option<DirectoryInfo>("--directory")
        {
            Description = "Published application directory (for example: bin/Release/<tfm>/publish)",
            Required = true
        };
        var fdOutput = new Option<FileInfo>("--output")
        {
            Description = $"Destination path for the generated {extension} file",
            Required = true
        };
        var fdMetadata = new MetadataOptionSet();

        var fromDirectoryCommand = new Command("from-directory", $"Create a {friendlyName} from a published application directory.");
        fromDirectoryCommand.Add(fdBuildDir);
        fromDirectoryCommand.Add(fdOutput);
        fdMetadata.AddTo(fromDirectoryCommand);
        if (defaultLayoutOption != null)
        {
            fromDirectoryCommand.Add(defaultLayoutOption);
        }

        var fdBinder = fdMetadata.CreateBinder(defaultLayoutOption);

        fromDirectoryCommand.SetAction(async parseResult =>
        {
            var directory = parseResult.GetValue(fdBuildDir)!;
            var output = parseResult.GetValue(fdOutput)!;
            var opts = fdBinder.Bind(parseResult);
            optionsPostProcessor?.Invoke(opts, parseResult);
            await ExecutionWrapper.ExecuteWithLogging(commandName, output.FullName, logger => handler(directory, output, opts, logger));
        });

        rootCommand.Add(fromDirectoryCommand);
        return new CommandSet(rootCommand, fromDirectoryCommand);
    }
}
