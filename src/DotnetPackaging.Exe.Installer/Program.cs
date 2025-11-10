using Avalonia;
using ReactiveUI.Avalonia;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.Installer;

internal static class Program
{
    public static void Main(string[] args)
    {
        try
        {
            // Headless test hook: dump payload metadata to a JSON file and exit
            var dumpPath = Environment.GetEnvironmentVariable("DP_DUMP_METADATA_JSON");
            var dumpRawPath = Environment.GetEnvironmentVariable("DP_DUMP_RAW_METADATA_JSON");
            if (!string.IsNullOrWhiteSpace(dumpPath) || !string.IsNullOrWhiteSpace(dumpRawPath))
            {
                try
                {
                    var payload = DotnetPackaging.Exe.Installer.Core.PayloadExtractor.LoadPayload();
                    if (payload.IsSuccess)
                    {
                        if (!string.IsNullOrWhiteSpace(dumpPath))
                        {
                            var json = System.Text.Json.JsonSerializer.Serialize(payload.Value.Metadata);
                            System.IO.File.WriteAllText(dumpPath, json);
                        }

                        if (!string.IsNullOrWhiteSpace(dumpRawPath))
                        {
                            using var stream = payload.Value.Content.ToStreamSeekable();
                            using var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: false);
                            var entry = zip.GetEntry("metadata.json");
                            if (entry is null)
                            {
                                System.IO.File.WriteAllText(dumpRawPath!, "ERROR: metadata.json missing");
                            }
                            else
                            {
                                using var es = entry.Open();
                                using var reader = new System.IO.StreamReader(es);
                                var text = reader.ReadToEnd();
                                System.IO.File.WriteAllText(dumpRawPath!, text);
                            }
                        }
                        return;
                    }

                    var errTarget = !string.IsNullOrWhiteSpace(dumpPath) ? dumpPath! : dumpRawPath!;
                    System.IO.File.WriteAllText(errTarget, $"ERROR: {payload.Error}");
                    return;
                }
                catch (Exception metaEx)
                {
                    var errTarget = !string.IsNullOrWhiteSpace(dumpPath) ? dumpPath! : dumpRawPath!;
                    System.IO.File.WriteAllText(errTarget, $"ERROR: {metaEx}");
                    return;
                }
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            try
            {
                var log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-installer-stub-error.txt");
                System.IO.File.WriteAllText(log, ex.ToString());
            }
            catch
            {
                // ignored
            }

            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
#if DEBUG
            .WithDeveloperTools()
#endif
            .UseReactiveUI();
}