namespace DotnetPackaging.InstallerStub;

public sealed record InstallationResult(InstallerMetadata Metadata, string InstallDirectory, string ExecutablePath);
