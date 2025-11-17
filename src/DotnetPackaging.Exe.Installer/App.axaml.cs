using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DotnetPackaging.Exe.Installer.Core;
using DotnetPackaging.Exe.Installer.Uninstallation;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.Installer;

public sealed class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
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

        var args = desktopLifetime.Args ?? Array.Empty<string>();
        if (IsUninstallMode(args))
        {
            await Uninstallation.Uninstallation.Launch();
            return;
        }

        await Installation.Installation.Launch();
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

    private static bool IsUninstallMode(IEnumerable<string> args)
        => args.Any(arg => string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase));
}