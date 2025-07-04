using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage.WIP;

public class AppImage(IRuntime runtime, UnixDirectory directory)
{
    public IRuntime Runtime { get; } = runtime;
    public UnixDirectory Directory { get; } = directory;
}