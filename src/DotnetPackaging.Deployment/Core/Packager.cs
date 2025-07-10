using DotnetPackaging.Deployment.Platforms.Android;
using DotnetPackaging.Deployment.Platforms.Linux;
using DotnetPackaging.Deployment.Platforms.Wasm;
using DotnetPackaging.Deployment.Platforms.Windows;

namespace DotnetPackaging.Deployment.Core;

public class Packager(IDotnet dotnet, Maybe<ILogger> logger)
{
    public Task<Result<IEnumerable<INamedByteSource>>> CreateWindowsPackages(Path path, WindowsDeployment.DeploymentOptions deploymentOptions)
    {
        return new WindowsDeployment(dotnet, path, deploymentOptions, logger).Create();
    }

    public Task<Result<IEnumerable<INamedByteSource>>> CreateAndroidPackages(Path path, AndroidDeployment.DeploymentOptions options)
    {
        return new AndroidDeployment(dotnet, path, options, logger).Create();
    }
    
    public Task<Result<IEnumerable<INamedByteSource>>> CreateLinuxPackages(Path path, AppImage.Metadata.AppImageMetadata metadata)
    {
        return new LinuxDeployment(dotnet, path, metadata, logger).Create();
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
        return dotnet.Publish(projectPath)
            .Bind(WasmApp.Create);
    }
}