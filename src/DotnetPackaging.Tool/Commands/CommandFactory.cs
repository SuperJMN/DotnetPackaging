using System.CommandLine;
using Serilog;

namespace DotnetPackaging.Tool.Commands;

public static class CommandFactory
{
    public static Command CreateCommand(
        string commandName,
        string friendlyName,
        string extension,
        Func<DirectoryInfo, FileInfo, Options, ILogger, Task> handler,
        string? description = null,
        Option<bool>? defaultLayoutOption = null,
        Action<Options, ParseResult>? optionsPostProcessor = null,
        params string[] aliases)
    {
        var buildDir = new Option<DirectoryInfo>("--directory")
        {
            Description = "Published application directory (for example: bin/Release/<tfm>/publish)",
            Required = true
        };
        var outputFileOption = new Option<FileInfo>("--output")
        {
            Description = $"Destination path for the generated {extension} file",
            Required = true
        };

        var metadata = new MetadataOptionSet();

        var defaultDescription = description ??
                                 $"Create a {friendlyName} from a directory with the published application contents. Everything is inferred. For .NET apps this is usually the 'publish' directory.";
        var fromBuildDir = new Command(commandName, defaultDescription);

        foreach (var alias in aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                fromBuildDir.Aliases.Add(alias);
            }
        }

        fromBuildDir.Add(buildDir);
        fromBuildDir.Add(outputFileOption);
        metadata.AddTo(fromBuildDir);
        if (defaultLayoutOption != null)
        {
            fromBuildDir.Add(defaultLayoutOption);
        }

        var options = metadata.CreateBinder(defaultLayoutOption);

        fromBuildDir.SetAction(async parseResult =>
        {
            var directory = parseResult.GetValue(buildDir)!;
            var output = parseResult.GetValue(outputFileOption)!;
            var opts = options.Bind(parseResult);
            optionsPostProcessor?.Invoke(opts, parseResult);
            await ExecutionWrapper.ExecuteWithLogging(commandName, output.FullName, logger => handler(directory, output, opts, logger));
        });
        return fromBuildDir;
    }
}
