namespace DotnetPackaging.Exe.Installer.Core;

public sealed record InstallerMetadata(
    string AppId,
    string ApplicationName,
    string Version,
    string Vendor,
    string? Description = null,
    string? ExecutableName = null,
    bool HasLogo = false);