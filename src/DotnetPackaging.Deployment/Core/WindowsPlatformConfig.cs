using DotnetPackaging.Deployment.Platforms.Windows;

namespace DotnetPackaging.Deployment.Core;

public class WindowsPlatformConfig
{
    public string ProjectPath { get; internal set; } = string.Empty;
    public WindowsDeployment.DeploymentOptions Options { get; internal set; } = null!;
}