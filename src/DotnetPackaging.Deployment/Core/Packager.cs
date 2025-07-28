using DotnetPackaging.Deployment.Platforms.Android;
using DotnetPackaging.Deployment.Platforms.Linux;
using DotnetPackaging.Deployment.Platforms.Wasm;
using DotnetPackaging.Deployment.Platforms.Windows;

namespace DotnetPackaging.Deployment.Core;

public class Packager(IDotnet dotnet, Maybe<ILogger> logger)
{
    public Task<Result<IEnumerable<INamedByteSource>>> CreateWindowsPackages(Path path, WindowsDeployment.DeploymentOptions deploymentOptions)
    {
        var platformLogger = logger.ForPlatform("Windows");
        return new WindowsDeployment(dotnet, path, deploymentOptions, platformLogger).Create();
    }

    public Task<Result<IEnumerable<INamedByteSource>>> CreateAndroidPackages(Path path, AndroidDeployment.DeploymentOptions options)
    {
        var platformLogger = logger.ForPlatform("Android");
        return new AndroidDeployment(dotnet, path, options, platformLogger).Create();
    }
    
    public Task<Result<IEnumerable<INamedByteSource>>> CreateLinuxPackages(Path path, AppImage.Metadata.AppImageMetadata metadata)
    {
        var platformLogger = logger.ForPlatform("Linux");
        return new LinuxDeployment(dotnet, path, metadata, platformLogger).Create();
    }
    
    public Task<Result<INamedByteSource>> CreateNugetPackage(Path path, string version)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path), "Cannot create a NuGet package from a null path.");
        }

        return dotnet.Pack(path, version);
    }
    
    public Task<Result<WasmApp>> CreateWasmSite(string projectPath)
    {
        var platformLogger = logger.ForPlatform("Wasm");
        var platformDotnet = new Dotnet(((Dotnet)dotnet).Command, platformLogger);
        return platformDotnet.Publish(projectPath)
            .Bind(WasmApp.Create);
    }
}