using DotnetPackaging.Deployment.Platforms.Android;

namespace DotnetPackaging.Deployment.Core;

public class AndroidPlatformConfig
{
    public string ProjectPath { get; internal set; } = string.Empty;
    public AndroidDeployment.DeploymentOptions Options { get; internal set; } = null!;
}