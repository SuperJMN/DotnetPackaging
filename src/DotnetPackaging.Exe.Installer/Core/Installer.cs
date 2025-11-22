using CSharpFunctionalExtensions;
using System.Text.Json;
using Serilog;
using Zafiro.DivineBytes;
using Path = System.IO.Path;

namespace DotnetPackaging.Exe.Installer.Core;

internal static class Installer
{
    public static Result<string> Install(string targetDir, InstallerMetadata meta, long payloadSizeBytes, Maybe<IByteSource> logo)
    {
        return ResolveMainExe(targetDir, meta)
            .Tap(exePath => ShortcutService.TryCreateStartMenuShortcut(meta.ApplicationName, exePath))
            .Tap(exePath => RegisterUninstaller(targetDir, meta, exePath, payloadSizeBytes, logo));
    }

    private static void RegisterUninstaller(string targetDir, InstallerMetadata meta, string mainExePath, long payloadSizeBytes, Maybe<IByteSource> logo)
    {
        if (Environment.ProcessPath is null)
        {
            return;
        }

        PersistMetadata(targetDir, meta);
        PersistLogo(targetDir, logo);

        // Strategy:
        // 1. Copy full installer as Uninstall.exe to "Uninstall" subdirectory to avoid DLL locks/conflicts with main app
        // 2. Registry points to Uninstall.exe in that subdirectory
        
        try
        {
            var uninstallDir = Path.Combine(targetDir, "Uninstall");
            Directory.CreateDirectory(uninstallDir);
            PersistMetadata(uninstallDir, meta);
            PersistLogo(uninstallDir, logo);

            var uninstallerPath = Path.Combine(uninstallDir, "Uninstall.exe");
            var slimUninstallerResult = UninstallerBuilder.CreateSlimCopy(Environment.ProcessPath, uninstallerPath);
            if (slimUninstallerResult.IsFailure)
            {
                Log.Warning("Slim uninstaller creation failed: {Error}. Using full installer copy instead.", slimUninstallerResult.Error);
                File.Copy(Environment.ProcessPath, uninstallerPath, overwrite: true);
            }
            Log.Information("Uninstaller copied to: {Path}", uninstallerPath);

            WindowsRegistryService.Register(
                meta.AppId,
                meta.ApplicationName,
                meta.Version,
                meta.Vendor,
                targetDir,
                $"\"{uninstallerPath}\" --uninstall",
                mainExePath,
                payloadSizeBytes);
        }
        catch (Exception ex)
        {
             Log.Error(ex, "RegisterUninstaller failed");
        }
    }

    private static void PersistMetadata(string directory, InstallerMetadata meta)
    {
        try
        {
            var metadataPath = Path.Combine(directory, "metadata.json");
            var json = JsonSerializer.Serialize(meta);
            File.WriteAllText(metadataPath, json);
        }
        catch
        {
            // Best effort
        }
    }

    private static void PersistLogo(string directory, Maybe<IByteSource> logo)
    {
        if (logo.HasNoValue)
        {
            return;
        }

        try
        {
            var logoPath = Path.Combine(directory, "logo.png");
            using var source = logo.Value.ToStreamSeekable();
            using var destination = File.Open(logoPath, FileMode.Create, FileAccess.Write, FileShare.None);
            source.CopyTo(destination);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to persist logo to {Directory}", directory);
        }
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
}
