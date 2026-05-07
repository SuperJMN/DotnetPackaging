using System.Diagnostics;

namespace DotnetPackaging.Dmg.Tests;

internal static class ExternalDmgValidationTools
{
    public static string? FindHdiutil()
    {
        return FindExecutable("hdiutil");
    }

    public static string? FindDmg2Img()
    {
        return FindExecutable("dmg2img");
    }

    public static string? FindHfsFsck()
    {
        return OperatingSystem.IsMacOS()
            ? FindExecutable("fsck_hfs") ?? FindExecutable("fsck.hfsplus")
            : FindExecutable("fsck.hfsplus") ?? FindExecutable("fsck_hfs");
    }

    public static async Task<ProcessResult> Run(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Unable to start '{fileName}'.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static string? FindExecutable(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

internal sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
