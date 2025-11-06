using System.Runtime.InteropServices;

namespace DotnetPackaging.InstallerStub;

internal static class Installer
{
    public static void Install(string contentDir, string targetDir, InstallerMetadata meta)
    {
        // 1) Delete previous install
        if (Directory.Exists(targetDir))
        {
            Directory.Delete(targetDir, true);
        }
        Directory.CreateDirectory(targetDir);

        // 2) Copy content
        CopyRecursive(contentDir, targetDir);

        // 3) Resolve main exe
        var exePath = ResolveMainExe(targetDir, meta);

        // 4) Create Start Menu shortcut (per-user)
        TryCreateShortcut(meta.ApplicationName, exePath);
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

    private static void CopyRecursive(string sourceDir, string destDir)
    {
        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, dir);
            Directory.CreateDirectory(Path.Combine(destDir, rel));
        }
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
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
