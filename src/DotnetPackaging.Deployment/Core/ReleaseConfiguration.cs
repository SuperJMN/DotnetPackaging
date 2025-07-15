namespace DotnetPackaging.Deployment.Core;

public class ReleaseConfiguration
{
    public string Version { get; internal set; } = string.Empty;
    public TargetPlatform Platforms { get; internal set; } = TargetPlatform.None;

    // Platform-specific configurations with their own project paths
    public WindowsPlatformConfig? WindowsConfig { get; internal set; }
    public AndroidPlatformConfig? AndroidConfig { get; internal set; }
    public LinuxPlatformConfig? LinuxConfig { get; internal set; }
    public WebAssemblyPlatformConfig? WebAssemblyConfig { get; internal set; }
}