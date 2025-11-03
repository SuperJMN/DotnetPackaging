using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using Zafiro.Mixins;

namespace DotnetPackaging.Flatpak;

public class FlatpakOptions
{
    // Flatpak base
    public string Runtime { get; init; } = "org.freedesktop.Platform";
    public string Sdk { get; init; } = "org.freedesktop.Sdk";
    public string Branch { get; init; } = "stable";
    public string RuntimeVersion { get; init; } = "24.08";

    // Permissions (Context)
    public IEnumerable<string> Shared { get; init; } = new[] { "network", "ipc" };
    public IEnumerable<string> Sockets { get; init; } = new[] { "wayland", "x11", "pulseaudio" };
    public IEnumerable<string> Devices { get; init; } = new[] { "dri" };
    public IEnumerable<string> Filesystems { get; init; } = new[] { "home" };

    // Overrides
    public Maybe<Architecture> ArchitectureOverride { get; init; } = Maybe<Architecture>.None;
    public Maybe<string> CommandOverride { get; init; } = Maybe<string>.None;
}
