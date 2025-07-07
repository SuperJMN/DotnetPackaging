using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage.Core;

public class AppImageContainer(IRuntime runtime, UnixDirectory container)
{
    public IRuntime Runtime { get; } = runtime;
    public UnixDirectory Container { get; } = container;
}