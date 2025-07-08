using DotnetPackaging.Deployment.Platforms.Android;
using DotnetPackaging.Deployment.Platforms.Linux;
using DotnetPackaging.Deployment.Platforms.Windows;

namespace DotnetPackaging.Deployment;

public class Packager(IDotnet dotnet, Maybe<ILogger> logger)
{
    public Task<Result<IEnumerable<INamedByteSource>>> CreateForWindows(Path path, WindowsDeployment.DeploymentOptions deploymentOptions)
    {
        return new WindowsDeployment(dotnet, path, deploymentOptions, logger).Create();
    }

    public Task<Result<IEnumerable<INamedByteSource>>> CreateForAndroid(Path path, AndroidDeployment.DeploymentOptions options)
    {
        return new AndroidDeployment(dotnet, path, options, logger).Create();
    }
    
    public Task<Result<IEnumerable<INamedByteSource>>> CreateForLinux(Path path, DotnetPackaging.AppImage.Metadata.AppImageMetadata metadata)
    {
        return new LinuxDeployment(dotnet, path, metadata, logger).Create();
    }
}