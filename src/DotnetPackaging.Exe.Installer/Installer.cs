using System.Runtime.InteropServices;
using System.Linq;
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

            var explicitExecutable = TryResolveExplicitExecutable(targetDir, meta.ExecutableName);
            if (explicitExecutable.HasValue)
            {
                return explicitExecutable.Value;
            }

            var candidates = EnumerateExecutableCandidates(targetDir);
            if (!candidates.Any())
            {
                throw new InvalidOperationException("No .exe found in installed content.");
            }

            var byApplicationName = TryMatchByApplicationName(candidates, meta.ApplicationName);
            return byApplicationName.Match(
                value => value,
                () => SelectBestExecutable(targetDir, candidates));
        }, ex => ex.Message);
    }

    private static Maybe<string> TryResolveExplicitExecutable(string targetDir, string? executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
        {
            return Maybe<string>.None;
        }

        var normalized = executableName.Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.Combine(targetDir, normalized);
        return File.Exists(candidate)
            ? Maybe<string>.From(candidate)
            : Maybe<string>.None;
    }

    private static IReadOnlyList<string> EnumerateExecutableCandidates(string targetDir)
    {
        return Directory.EnumerateFiles(targetDir, "*.exe", SearchOption.AllDirectories)
            .Where(candidate => !string.Equals(Path.GetFileName(candidate), "createdump.exe", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static Maybe<string> TryMatchByApplicationName(IEnumerable<string> candidates, string applicationName)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            return Maybe<string>.None;
        }

        var normalizedApplicationName = NormalizeExecutableStem(applicationName);
        var match = candidates
            .Select(candidate => new
            {
                Path = candidate,
                Stem = NormalizeExecutableStem(System.IO.Path.GetFileNameWithoutExtension(candidate))
            })
            .FirstOrDefault(candidate => string.Equals(candidate.Stem, normalizedApplicationName, StringComparison.OrdinalIgnoreCase));

        return match is null ? Maybe<string>.None : Maybe<string>.From(match.Path);
    }

    private static string SelectBestExecutable(string targetDir, IReadOnlyCollection<string> candidates)
    {
        return candidates
            .Select(candidate => new
            {
                Path = candidate,
                Depth = GetDepth(targetDir, candidate),
                Length = candidate.Length
            })
            .OrderBy(candidate => candidate.Depth)
            .ThenBy(candidate => candidate.Length)
            .First()
            .Path;
    }

    private static int GetDepth(string root, string candidate)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return relative.Count(ch => ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar);
    }

    private static string NormalizeExecutableStem(string text)
    {
        return new string(text.Where(char.IsLetterOrDigit).ToArray());
    }

    private static void TryCreateShortcut(string appName, string targetExe)
    {
        try
        {
            var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var shortcutName = BuildShortcutName(appName, targetExe);
            var lnkPath = Path.Combine(programs, $"{shortcutName}.lnk");
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
