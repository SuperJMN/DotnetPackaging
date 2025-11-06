namespace DotnetPackaging.InstallerStub;

public sealed record InstallerMetadata(
    string AppId,
    string ApplicationName,
    string Version,
    string Vendor,
    string? Description = null,
    string? ExecutableName = null);