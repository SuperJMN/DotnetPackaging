using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage.WIP;

public class AppImage(IRuntime runtime, UnixDirectory container)
{
    public IRuntime Runtime { get; } = runtime;
    public UnixDirectory Container { get; } = container;
}