namespace DotnetPackaging.Exe.Installer;

public sealed record InstallationResult(InstallerMetadata Metadata, string InstallDirectory, string ExecutablePath);
