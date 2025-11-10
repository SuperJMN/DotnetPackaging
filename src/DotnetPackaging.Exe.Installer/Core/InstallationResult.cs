namespace DotnetPackaging.Exe.Installer.Core;

public sealed record InstallationResult(InstallerMetadata Metadata, string InstallDirectory, string ExecutablePath);
