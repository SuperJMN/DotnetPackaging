using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Exe.Installer.Core;

internal static class InstallationRegistry
{
    private static readonly object Gate = new();

    public static Result Register(InstallationResult installation)
    {
        return Result.Try(() =>
        {
            lock (Gate)
            {
                Directory.CreateDirectory(GetRegistryDirectory());
                var path = GetRegistryPath(installation.Metadata.AppId);
                var record = new RegisteredInstallation(
                    installation.Metadata.AppId,
                    installation.Metadata.ApplicationName,
                    installation.Metadata.Vendor,
                    installation.Metadata.Version,
                    installation.InstallDirectory,
                    installation.ExecutablePath);
                var json = JsonSerializer.Serialize(record);
                File.WriteAllText(path, json);
            }
        }, ex => $"Failed to register installation: {ex.Message}");
    }

    public static Result<RegisteredInstallation> Get(string appId)
    {
        return Result.Try(() =>
        {
            lock (Gate)
            {
                var path = GetRegistryPath(appId);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"Installation record for '{appId}' was not found.", path);
                }

                var json = File.ReadAllText(path);
                var record = JsonSerializer.Deserialize<RegisteredInstallation>(json);
                return record ?? throw new InvalidOperationException($"Installation record for '{appId}' is invalid.");
            }
        }, ex => $"Failed to read installation information: {ex.Message}");
    }

    public static Result Remove(string appId)
    {
        return Result.Try(() =>
        {
            lock (Gate)
            {
                var path = GetRegistryPath(appId);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }, ex => $"Failed to remove installation information: {ex.Message}");
    }

    private static string GetRegistryDirectory()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDirectory, "DotnetPackaging", "Installations");
    }

    private static string GetRegistryPath(string appId)
    {
        var directory = GetRegistryDirectory();
        var fileName = BuildFileName(appId);
        return Path.Combine(directory, fileName);
    }

    private static string BuildFileName(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return "default.json";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(appId.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(appId));
            sanitized = Convert.ToHexString(hash)[..16];
        }

        return sanitized + ".json";
    }
}
