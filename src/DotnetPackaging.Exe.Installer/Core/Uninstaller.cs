using System.IO;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Exe.Installer.Core;

internal static class Uninstaller
{
    public static Result<UninstallationResult> Uninstall(InstallerMetadata metadata, RegisteredInstallation installation)
    {
        return Result.Try(() =>
            {
                ShortcutService.TryDeleteDesktopShortcut(metadata.ApplicationName, installation.ExecutablePath);
                ShortcutService.TryDeleteStartMenuShortcut(metadata.ApplicationName, installation.ExecutablePath);
                return new UninstallationResult(metadata, installation.InstallDirectory);
            }, ex => $"Failed to uninstall application: {ex.Message}")
            .Bind(result => InstallationRegistry.Remove(metadata.AppId).Map(() => result))
            .Tap(() => WindowsRegistryService.Remove(metadata.AppId));
    }
}
