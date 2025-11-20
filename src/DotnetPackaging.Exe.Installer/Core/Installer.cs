using CSharpFunctionalExtensions;
using System.Text.Json;
using Serilog;

namespace DotnetPackaging.Exe.Installer.Core;

internal static class Installer
{
    public static Result<string> Install(string targetDir, InstallerMetadata meta)
    {
        return ResolveMainExe(targetDir, meta)
            .Tap(exePath => ShortcutService.TryCreateStartMenuShortcut(meta.ApplicationName, exePath))
            .Tap(exePath => RegisterUninstaller(targetDir, meta, exePath));
    }

    private static void RegisterUninstaller(string targetDir, InstallerMetadata meta, string mainExePath)
    {
        if (Environment.ProcessPath is null)
        {
            return;
        }

        PersistMetadata(targetDir, meta);

        // Use launcher strategy to avoid .NET single-file extraction issues:
        // 1. Copy full installer as Uninstall.exe to installation directory
        // 2. Copy launcher as UninstallLauncher.exe to installation directory
        // 3. Registry points to UninstallLauncher.exe
        // 4. Launcher copies Uninstall.exe to %TEMP% and runs it from there
        
        try
        {
            // Copy installer as uninstaller but remove embedded payload to keep it lean
            var uninstallerPath = Path.Combine(targetDir, "Uninstall.exe");
            var uninstallerResult = UninstallerBuilder.CreatePayloadlessUninstaller(Environment.ProcessPath, uninstallerPath);
            if (uninstallerResult.IsFailure)
            {
                Log.Warning("{Message}. Falling back to raw copy.", uninstallerResult.Error);
                File.Copy(Environment.ProcessPath, uninstallerPath, overwrite: true);
            }
            else
            {
                Log.Information("Payload removed from uninstaller at: {Path}", uninstallerResult.Value);
            }
            
            // Extract launcher from embedded resource and save to installation directory
            var launcherPath = Path.Combine(targetDir, "UninstallLauncher.exe");
            var launcherExtracted = TryExtractLauncher(launcherPath);
            
            if (!launcherExtracted)
            {
                Log.Warning("Launcher not found in resources, registry will point directly to uninstaller");
                // Fallback: point directly to uninstaller (may have extraction issues)
                WindowsRegistryService.Register(
                    meta.AppId, 
                    meta.ApplicationName, 
                    meta.Version, 
                    meta.Vendor, 
                    targetDir, 
                    $"\"{uninstallerPath}\" --uninstall", 
                    mainExePath);
            }
            else
            {
                Log.Information("Launcher copied to installation directory: {Path}", launcherPath);
                // Point registry to launcher
                WindowsRegistryService.Register(
                    meta.AppId, 
                    meta.ApplicationName, 
                    meta.Version, 
                    meta.Vendor, 
                    targetDir, 
                    $"\"{launcherPath}\"", 
                    mainExePath);
            }
        }
        catch (Exception ex)
        {
             Log.Error(ex, "RegisterUninstaller failed");
        }
    }

    private static void PersistMetadata(string targetDir, InstallerMetadata meta)
    {
        try
        {
            var metadataPath = Path.Combine(targetDir, "metadata.json");
            var json = JsonSerializer.Serialize(meta);
            File.WriteAllText(metadataPath, json);
            Log.Information("Installer metadata persisted for uninstaller at {Path}", metadataPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to persist installer metadata for uninstaller");
        }
    }
    
    private static bool TryExtractLauncher(string targetPath)
    {
        try
        {
            // Try to find launcher in embedded resources
            var assembly = typeof(Installer).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.Contains("UninstallLauncher.exe"));
            
            if (resourceName is null)
            {
                return false;
            }
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return false;
            }
            
            using var fileStream = File.Create(targetPath);
            stream.CopyTo(fileStream);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to extract launcher");
            return false;
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
