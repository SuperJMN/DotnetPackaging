using DotnetPackaging.AppImage.Core;
using Zafiro.DivineBytes.Permissioned;

namespace DotnetPackaging.AppImage.WIP;

public class AppImage(IRuntime runtime, UnixDirectory directory)
{
    public IRuntime Runtime { get; } = runtime;
    public UnixDirectory Directory { get; } = directory;
}