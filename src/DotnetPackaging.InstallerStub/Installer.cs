using System.Runtime.InteropServices;

namespace DotnetPackaging.InstallerStub;

internal static class Installer
{
    public static string Install(string targetDir, InstallerMetadata meta)
    {
        if (!Directory.Exists(targetDir))
        {
            throw new DirectoryNotFoundException($"Installation directory '{targetDir}' was not found.");
        }

        var exePath = ResolveMainExe(targetDir, meta);
        TryCreateShortcut(meta.ApplicationName, exePath);
        return exePath;
    }

    private static string ResolveMainExe(string targetDir, InstallerMetadata meta)
    {
        if (!string.IsNullOrWhiteSpace(meta.ExecutableName))
        {
            var candidate = Path.Combine(targetDir, meta.ExecutableName);
            if (File.Exists(candidate)) return candidate;
        }

        var firstExe = Directory.EnumerateFiles(targetDir, "*.exe", SearchOption.AllDirectories)
            .OrderBy(p => p.Length)
            .FirstOrDefault();
        if (firstExe is null)
            throw new InvalidOperationException("No .exe found in installed content.");
        return firstExe;
    }

    private static void TryCreateShortcut(string appName, string targetExe)
    {
        try
        {
            var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var lnkPath = Path.Combine(programs, $"{appName}.lnk");
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
}
