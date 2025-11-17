namespace DotnetPackaging.Exe.Installer.Core;

public sealed record UninstallationResult(InstallerMetadata Metadata, string InstallDirectory);
