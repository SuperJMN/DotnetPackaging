namespace DotnetPackaging.Exe.Installer.Core;

public sealed record RegisteredInstallation(
    string AppId,
    string ApplicationName,
    string Vendor,
    string Version,
    string InstallDirectory,
    string ExecutablePath);
