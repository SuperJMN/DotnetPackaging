using System.IO;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Exe.Installer.Core;

internal static class Uninstaller
{
    public static Result<UninstallationResult> Uninstall(InstallerMetadata metadata, RegisteredInstallation installation)
    {
        return Result.Try(() =>
            {
                RemoveInstallationDirectory(installation.InstallDirectory);
                ShortcutService.TryDeleteDesktopShortcut(metadata.ApplicationName, installation.ExecutablePath);
                ShortcutService.TryDeleteStartMenuShortcut(metadata.ApplicationName, installation.ExecutablePath);
                return new UninstallationResult(metadata, installation.InstallDirectory);
            }, ex => $"Failed to uninstall application: {ex.Message}")
            .Bind(result => InstallationRegistry.Remove(metadata.AppId).Map(() => result))
            .Tap(() => WindowsRegistryService.Remove(metadata.AppId));
    }

    private static void RemoveInstallationDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var runningDir = Environment.ProcessPath is not null 
            ? Path.GetDirectoryName(Environment.ProcessPath) 
            : null;

        foreach (var directory in Directory.GetDirectories(path))
        {
            // If we are running from a subdirectory (e.g. "Uninstall"), don't delete it yet.
            // It will be removed by SelfDestruct.
            if (runningDir is not null && string.Equals(directory, runningDir, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try 
            {
                Directory.Delete(directory, true);
            }
            catch
            {
                // Best effort
            }
        }

        foreach (var file in Directory.GetFiles(path))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Best effort
            }
        }
        
        // We don't delete 'path' itself here because it might contain the runningDir.
        // SelfDestruct will handle the final cleanup of 'path'.
    }
}
