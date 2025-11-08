using DotnetPackaging.Exe.Installer.Steps.Welcome;
using ReactiveUI;

namespace DotnetPackaging.Exe.Installer.Steps;

public sealed class WelcomeViewModel(InstallerMetadata metadata) : ReactiveObject, IWelcomeViewModel
{
    public InstallerMetadata Metadata { get; } = metadata;

    public string Heading => Metadata is { } meta
        ? $"Welcome to the {meta.ApplicationName} wizard"
        : "Welcome to the installation wizard";
}
