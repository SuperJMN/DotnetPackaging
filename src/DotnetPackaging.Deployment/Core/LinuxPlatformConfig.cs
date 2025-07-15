using DotnetPackaging.AppImage.Metadata;

namespace DotnetPackaging.Deployment.Core;

public class LinuxPlatformConfig
{
    public string ProjectPath { get; internal set; } = string.Empty;
    public AppImageMetadata Metadata { get; internal set; } = null!;
}