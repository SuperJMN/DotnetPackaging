using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Exe.Installer;

internal static class Installer
{
    public static Result<string> Install(string targetDir, InstallerMetadata meta)
    {
        return ResolveMainExe(targetDir, meta)
            .Tap(exePath => TryCreateShortcut(meta.ApplicationName, exePath));
    }

    private static Result<string> ResolveMainExe(string targetDir, InstallerMetadata meta)
    {
        return Result.Try(() =>
        {
            if (!Directory.Exists(targetDir))
            {
                throw new DirectoryNotFoundException($"Installation directory '{targetDir}' was not found.");
            }

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
        }, ex => ex.Message);
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
