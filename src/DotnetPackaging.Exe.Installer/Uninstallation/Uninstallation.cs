using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DotnetPackaging.Exe.Installer.Core;
using DotnetPackaging.Exe.Installer.Uninstallation.Wizard;
using Zafiro.Avalonia.Dialogs.Implementations;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;

namespace DotnetPackaging.Exe.Installer.Uninstallation;

internal static class Uninstallation
{
    public static async Task Launch()
    {
        var currentApp = App.Current;
        if (currentApp?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            return;
        }

        if (desktopLifetime.MainWindow is null)
        {
            Serilog.Log.Error("MainWindow not set");
            return;
        }

        try
        {
            Serilog.Log.Information("Uninstallation launched");

            var dialog = new DesktopDialog();
            var payload = ResolvePayload();
            var wizard = new UninstallWizard(payload).CreateWizard();

            Serilog.Log.Information("Wizard created, resolving title");
            var title = await ResolveWindowTitle(payload);
            Serilog.Log.Information("Title resolved: {Title}", title);
            
            await wizard.ShowInDialog(dialog, title);
            Serilog.Log.Information("Wizard dialog finished");

            await TryTriggerSelfDestruct(payload);
            Serilog.Log.Information("Self-destruct triggered");
        }
        catch (Exception ex)
        {
            Serilog.Log.Fatal(ex, "Uninstallation failed");
            TryWriteCrashLog(ex);
            throw;
        }
    }

    private static IInstallerPayload ResolvePayload()
    {
        var payloadResult = MetadataFilePayload.FromProcessDirectory();
        if (payloadResult.IsSuccess)
        {
            Serilog.Log.Information("Using metadata.json from installation directory");
            return payloadResult.Value;
        }

        Serilog.Log.Warning("metadata.json not found: {Error}. Using fallback payload", payloadResult.Error);
        // For uninstaller, we create a minimal payload that only provides metadata reading from disk
        // If that fails, DefaultInstallerPayload will try to load embedded resources which won't exist
        // but that's OK - the wizard will handle missing metadata gracefully
        return new DefaultInstallerPayload();
    }

    private static async Task TryTriggerSelfDestruct(IInstallerPayload payload)
    {
        var meta = await payload.GetMetadata();
        if (meta.IsFailure)
        {
            return;
        }

        var appId = meta.Value.AppId;
        var installation = InstallationRegistry.Get(appId);
        
        // If installation is missing, it means it was successfully removed (or never existed)
        // Check if we are running from the typical install location to decide if we should self-destruct
        if (installation.IsFailure && Environment.ProcessPath is { } path)
        {
            var directory = Path.GetDirectoryName(path);
            if (directory != null)
            {
                // We schedule self-destruct.
                // If we are in "Uninstall" subdirectory, we want to remove the parent (root) as well.
                if (Path.GetFileName(directory).Equals("Uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = Directory.GetParent(directory);
                    if (parent != null && IsDefaultInstallRoot(meta.Value, parent.FullName))
                    {
                        // Delete the parent (Installation Root) which contains the Uninstall folder
                        SelfDestruct.Schedule(path, parent.FullName);
                        return;
                    }
                }

                // Fallback: just delete the directory we are in
                SelfDestruct.Schedule(path, directory);
            }
        }
    }

    private static bool IsDefaultInstallRoot(InstallerMetadata metadata, string directory)
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs");
        var vendorPart = SanitizePathPart(metadata.Vendor);
        var appPart = SanitizePathPart(metadata.ApplicationName);

        var expectedRoot = string.Equals(vendorPart, appPart, StringComparison.OrdinalIgnoreCase) ||
                           string.IsNullOrWhiteSpace(vendorPart)
            ? Path.Combine(baseDir, appPart)
            : Path.Combine(baseDir, vendorPart, appPart);

        return string.Equals(directory, expectedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizePathPart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "App";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(text.Where(c => !invalid.Contains(c)).ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "App" : sanitized;
    }


    private static async Task<string> ResolveWindowTitle(IInstallerPayload payload)
    {
        var metaResult = await payload.GetMetadata();
        if (metaResult.IsFailure)
        {
            Serilog.Log.Warning("Metadata load failed: {Error}", metaResult.Error);
        }
        
        var title = metaResult.IsSuccess && !string.IsNullOrWhiteSpace(metaResult.Value.ApplicationName)
            ? $"{metaResult.Value.ApplicationName} Uninstaller"
            : "Uninstaller";

        return title;
    }

    private static void TryWriteCrashLog(Exception ex)
    {
        try
        {
            var log = Path.Combine(Path.GetTempPath(), "dp-installer-stub-error.txt");
            File.WriteAllText(log, ex.ToString());
        }
        catch
        {
            // ignored
        }
    }
}
