namespace DotnetPackaging.Deployment.Core;

[Flags]
public enum TargetPlatform
{
    None = 0,
    Windows = 1,
    Linux = 2,
    MacOs = 4,
    Android = 8,
    WebAssembly = 16,
    All = Windows | Linux | MacOs | Android | WebAssembly
}
