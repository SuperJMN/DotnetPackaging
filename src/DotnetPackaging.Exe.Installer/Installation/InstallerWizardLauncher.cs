using System.IO.Compression;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DotnetPackaging.Exe.Installer.Core;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using Projektanker.Icons.Avalonia.MaterialDesign;
using Zafiro.Avalonia.Dialogs.Implementations;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.DivineBytes;
using Path = System.IO.Path;

namespace DotnetPackaging.Exe.Installer;

internal static class Installation
{
    public static async Task Launch(IApplicationLifetime? lifetime)
    {
        if (TryHandleMetadataDump())
        {
            (lifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            return;
        }

        if (lifetime is not IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            return;
        }

        try
        {
            RegisterIcons();

            var root = CreateRootWindow();
            desktopLifetime.MainWindow = root;
            root.Show();

            var dialog = new DesktopDialog();
            var folderPicker = new AvaloniaFolderPickerService(root.StorageProvider);
            var payload = new DefaultInstallerPayload();
            var wizard = new InstallWizard(folderPicker, payload).CreateWizard();

            var title = await ResolveWindowTitle(payload);
            await wizard.ShowInDialog(dialog, title);

            desktopLifetime.Shutdown();
        }
        catch (Exception ex)
        {
            TryWriteCrashLog(ex);
            throw;
        }
    }

    private static void RegisterIcons()
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>()
            .Register<MaterialDesignIconProvider>();
    }

    private static Window CreateRootWindow()
        => new()
        {
            Width = 1000,
            Height = 1000,
            Opacity = 0,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            ShowInTaskbar = false
        };

    private static async Task<string> ResolveWindowTitle(DefaultInstallerPayload payload)
    {
        var metaResult = await payload.GetMetadata();
        var title = metaResult.IsSuccess && !string.IsNullOrWhiteSpace(metaResult.Value.ApplicationName)
            ? $"{metaResult.Value.ApplicationName} Installer"
            : "Installer";

        return title;
    }

    private static bool TryHandleMetadataDump()
    {
        var dumpPath = Environment.GetEnvironmentVariable("DP_DUMP_METADATA_JSON");
        var dumpRawPath = Environment.GetEnvironmentVariable("DP_DUMP_RAW_METADATA_JSON");

        if (string.IsNullOrWhiteSpace(dumpPath) && string.IsNullOrWhiteSpace(dumpRawPath))
        {
            return false;
        }

        try
        {
            var payload = PayloadExtractor.LoadPayload();
            if (payload.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(dumpPath))
                {
                    var json = JsonSerializer.Serialize(payload.Value.Metadata);
                    File.WriteAllText(dumpPath!, json);
                }

                if (!string.IsNullOrWhiteSpace(dumpRawPath))
                {
                    using var stream = payload.Value.Content.ToStreamSeekable();
                    using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
                    var entry = zip.GetEntry("metadata.json");
                    if (entry is null)
                    {
                        File.WriteAllText(dumpRawPath!, "ERROR: metadata.json missing");
                    }
                    else
                    {
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream);
                        var text = reader.ReadToEnd();
                        File.WriteAllText(dumpRawPath!, text);
                    }
                }

                return true;
            }

            var errTarget = !string.IsNullOrWhiteSpace(dumpPath) ? dumpPath! : dumpRawPath!;
            File.WriteAllText(errTarget, $"ERROR: {payload.Error}");
            return true;
        }
        catch (Exception metaEx)
        {
            var errTarget = !string.IsNullOrWhiteSpace(dumpPath) ? dumpPath! : dumpRawPath!;
            File.WriteAllText(errTarget, $"ERROR: {metaEx}");
            return true;
        }
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
