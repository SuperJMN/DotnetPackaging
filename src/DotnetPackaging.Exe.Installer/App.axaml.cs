using System.IO.Compression;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DotnetPackaging.Exe.Installer.Core;
using Serilog;
using Zafiro.DivineBytes;
using Uninstaller = DotnetPackaging.Exe.Installer.Flows.Uninstallation.Uninstaller;

namespace DotnetPackaging.Exe.Installer;

public sealed class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (TryHandleMetadataDump())
        {
            (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            return;
        }

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            return;
        }

        base.OnFrameworkInitializationCompleted();

        // Create and set MainWindow immediately - this keeps the app running
        var mainWindow = new Window
        {
            Width = 1,
            Height = 1,
            Opacity = 0,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            ShowInTaskbar = false
        };
        desktopLifetime.MainWindow = mainWindow;

        // Launch installer/uninstaller after the window is shown
        mainWindow.Opened += async (_, _) => await LaunchFlow(desktopLifetime.Args ?? Array.Empty<string>(), mainWindow);
    }

    private static async Task LaunchFlow(IEnumerable<string> args, Window mainWindow)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("DP_FORCE_DISPATCHER_FAILURE") == "1")
            {
                throw new InvalidOperationException("Dispatcher initialization failed by request.");
            }

            if (IsUninstallMode(args))
            {
                await Uninstaller.Launch();
            }
            else
            {
                await Flows.Installation.Installer.Launch();
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to start installer dispatcher");
            throw;
        }
        finally
        {
            // Close the main window when installation/uninstallation is done
            mainWindow.Close();
        }
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
                    using var zip = new ZipArchive(stream, ZipArchiveMode.Read, false);
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

    private static bool IsUninstallMode(IEnumerable<string> args)
    {
        return args.Any(arg => string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase));
    }
}