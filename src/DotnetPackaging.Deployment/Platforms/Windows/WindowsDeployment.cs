using Zafiro.CSharpFunctionalExtensions;

namespace DotnetPackaging.Deployment.Platforms.Windows;

public class WindowsDeployment(IDotnet dotnet, Path projectPath, WindowsDeployment.DeploymentOptions options, Maybe<ILogger> logger)
{
    private static readonly Dictionary<Architecture, (string Runtime, string Suffix)> WindowsArchitecture = new()
    {
        [Architecture.X64] = ("win-x64", "x64"),
        [Architecture.Arm64] = ("win-arm64", "arm64")
    };
    
    public Task<Result<IEnumerable<INamedByteSource>>> Create()
    {
        IEnumerable<Architecture> supportedArchitectures = [Architecture.Arm64, Architecture.X64];

        return supportedArchitectures
            .Select(architecture => CreateFor(architecture, options).Tap(() => logger.Tap(l => l.Information("Publishing .exe for {Architecture}", architecture))))
            .CombineInOrder();
    }

    private Task<Result<INamedByteSource>> CreateFor(Architecture architecture, DeploymentOptions deploymentOptions)
    {
        var args = CreateArgs(architecture, deploymentOptions);
        var finalName = deploymentOptions.PackageName + "-" + deploymentOptions.Version + "-windows-" + $"{WindowsArchitecture[architecture].Suffix}" + ".exe";
        
        return dotnet.Publish(projectPath, args)
            .Bind(directory => directory.ResourcesRecursive()
                .TryFirst(file => file.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                .ToResult($"Can't find any .exe file in publish result directory {directory}"))
            .Map(INamedByteSource (file) => new Resource(finalName, file));
    }
    
    private static string CreateArgs(Architecture architecture, DeploymentOptions deploymentOptions)
    {
        IEnumerable<string[]> options =
        [
            ["configuration", "Release"],
            ["self-contained", "true"],
            ["runtime", WindowsArchitecture[architecture].Runtime]
        ];

        IEnumerable<string[]> properties =
        [
            ["PublishSingleFile", "true"],
            ["Version", deploymentOptions.Version],
            ["IncludeNativeLibrariesForSelfExtract", "true"],
            ["IncludeAllContentForSelfExtract", "true"],
            ["DebugType", "embedded"],
            ["Version", deploymentOptions.Version]
        ];

        return ArgumentsParser.Parse(options, properties);
    }

    public class DeploymentOptions
    {
        public required string Version { get; set; }
        public required string PackageName { get; set; }
    }
}