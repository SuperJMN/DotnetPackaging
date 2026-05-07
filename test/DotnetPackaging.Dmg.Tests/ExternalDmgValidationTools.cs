using System.Diagnostics;
using System.Xml.Linq;

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

    public static IReadOnlyList<HdiutilDevice> ParseHdiutilDevices(string plist)
    {
        var document = XDocument.Parse(plist);
        var rootDictionary = document.Root?.Element("dict");
        var entities = FindValue(rootDictionary, "system-entities");
        if (entities?.Name != "array")
        {
            return [];
        }

        return entities.Elements("dict")
            .Select(device => new HdiutilDevice(
                ReadString(device, "dev-entry"),
                ReadString(device, "mount-point"),
                ReadString(device, "content-hint")))
            .Where(device => !string.IsNullOrWhiteSpace(device.Device))
            .ToArray();
    }

    private static XElement? FindValue(XElement? dictionary, string key)
    {
        if (dictionary == null)
        {
            return null;
        }

        var elements = dictionary.Elements().ToArray();
        for (var i = 0; i < elements.Length - 1; i++)
        {
            if (elements[i].Name == "key" && elements[i].Value == key)
            {
                return elements[i + 1];
            }
        }

        return null;
    }

    private static string? ReadString(XElement dictionary, string key)
    {
        var value = FindValue(dictionary, key);
        return value?.Name == "string" ? value.Value : null;
    }

    private static string? FindExecutable(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var directories = path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Concat(["/sbin", "/usr/sbin", "/bin", "/usr/bin"])
            .Distinct();

        foreach (var directory in directories)
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

internal sealed record HdiutilDevice(string? Device, string? MountPoint, string? ContentHint);
