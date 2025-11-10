using System;
using System.IO;
using System.Linq;

namespace DotnetPackaging.Exe.Installer.Core;

internal static class ShortcutService
{
    public static void TryCreateStartMenuShortcut(string appName, string targetExe)
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        TryCreateShortcut(programs, appName, targetExe);
    }

    public static void TryCreateDesktopShortcut(string appName, string targetExe)
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            TryCreateShortcut(desktop, appName, targetExe);
        }
        catch
        {
            // Best-effort shortcut
        }
    }

    private static void TryCreateShortcut(string directory, string appName, string targetExe)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory)) return;

            Directory.CreateDirectory(directory);
            var shortcutName = BuildShortcutName(appName, targetExe);
            var lnkPath = Path.Combine(directory, $"{shortcutName}.lnk");
            Type shellType = Type.GetTypeFromProgID("WScript.Shell")!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            shortcut.TargetPath = targetExe;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe);
            shortcut.Save();
        }
        catch
        {
            // Best-effort shortcut
        }
    }

    private static string BuildShortcutName(string appName, string targetExe)
    {
        var desiredName = string.IsNullOrWhiteSpace(appName)
            ? Path.GetFileNameWithoutExtension(targetExe)
            : appName.Trim();

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(desiredName.Where(character => !invalidCharacters.Contains(character)).ToArray());

        return string.IsNullOrWhiteSpace(sanitized)
            ? Path.GetFileNameWithoutExtension(targetExe)
            : sanitized;
    }
}
