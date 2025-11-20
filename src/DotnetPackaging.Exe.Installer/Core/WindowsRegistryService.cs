using System;
using System.Runtime.Versioning;
using CSharpFunctionalExtensions;
using Microsoft.Win32;

namespace DotnetPackaging.Exe.Installer.Core;

[SupportedOSPlatform("windows")]
internal static class WindowsRegistryService
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    public static Result Register(string appId, string applicationName, string version, string publisher, string installLocation, string uninstallString, string displayIcon, long estimatedSizeBytes)
    {
        return Result.Try(() =>
        {
            using var key = Registry.CurrentUser.CreateSubKey(System.IO.Path.Combine(UninstallKey, appId));
            key.SetValue("DisplayName", applicationName);
            key.SetValue("DisplayVersion", version);
            key.SetValue("Publisher", publisher);
            key.SetValue("InstallLocation", installLocation);
            key.SetValue("UninstallString", uninstallString);
            key.SetValue("DisplayIcon", displayIcon);
            key.SetValue("NoModify", 1);
            key.SetValue("NoRepair", 1);

            if (estimatedSizeBytes > 0)
            {
                var estimatedSizeKb = (int)Math.Clamp((estimatedSizeBytes + 1023) / 1024, 1, int.MaxValue);
                key.SetValue("EstimatedSize", estimatedSizeKb, RegistryValueKind.DWord);
            }
        }, ex => $"Failed to create registry keys: {ex.Message}");
    }

    public static Result Remove(string appId)
    {
        return Result.Try(() =>
        {
            Registry.CurrentUser.DeleteSubKeyTree(System.IO.Path.Combine(UninstallKey, appId), throwOnMissingSubKey: false);
        }, ex => $"Failed to remove registry keys: {ex.Message}");
    }
}
