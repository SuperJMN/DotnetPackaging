using Zafiro.DivineBytes.System.IO;

namespace DotnetPackaging.Deployment.Core;

public class Dotnet : IDotnet
{
    public ICommand Command { get; }
    private readonly Maybe<ILogger> logger;
    private readonly System.IO.Abstractions.FileSystem filesystem = new();

    public Dotnet(ICommand command, Maybe<ILogger> logger)
    {
        Command = command;
        this.logger = logger;
    }
    
    public Task<Result<IContainer>> Publish(string projectPath, string arguments = "")
    {
        return Result.Try(() => filesystem.Directory.CreateTempSubdirectory())
            .Bind(outputDir =>
            {
                IEnumerable<string[]> options =
                [
                    ["output", outputDir.FullName],
                ];

                var implicitArguments = ArgumentsParser.Parse(options, []);

                var finalArguments = string.Join(" ", "publish", projectPath, arguments, implicitArguments);

                return Command.Execute("dotnet", finalArguments).Map(IContainer () =>new DirectoryContainer(outputDir) );
            });
    }

    public Task<Result> Push(string packagePath, string apiKey)
    {
        var args = string.Join(
            " ",
            "nuget push",
            packagePath,
            "--source https://api.nuget.org/v3/index.json",
            "--api-key",
            apiKey,
            "--skip-duplicate");

        return Command.Execute("dotnet", args);
    }

    public Task<Result<INamedByteSource>> Pack(string projectPath, string version)
    {
        if (projectPath == null)
        {
            throw new ArgumentNullException(nameof(projectPath), "Project path to pack cannot be null.");
        }

        return Result.Try(() => filesystem.Directory.CreateTempSubdirectory())
            .Map(async outputDir =>
            {
                var arguments = ArgumentsParser.Parse([
                    ["output", outputDir.FullName],
                ], [["version", version]]);
                await Command.Execute("dotnet", string.Join(" ", "pack", projectPath, arguments));
                return new DirectoryContainer(outputDir);
            })
            .Map(directory => directory.ResourcesRecursive())
            .Bind(sources => sources.TryFirst(file => file.Name.EndsWith(".nupkg")).ToResult("Cannot find any NuGet package in the output folder"));
    }
}