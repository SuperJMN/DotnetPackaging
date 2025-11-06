using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Exe;

public static class ResourceExePacker
{
    public static async Task<Result> Build(string stubPath, string publishDir, InstallerMetadata metadata, string outputPath)
    {
        try
        {
            if (!File.Exists(stubPath))
                return Result.Failure($"Stub not found: {stubPath}");
            if (!Directory.Exists(publishDir))
                return Result.Failure($"Publish directory not found: {publishDir}");

            // Copy stub to output first (we'll update resources in-place)
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.Copy(stubPath, outputPath, overwrite: true);

            // Build payload zip in temp
            var tmp = Path.Combine(Path.GetTempPath(), "dp-exe-rsrc-" + Guid.NewGuid());
            Directory.CreateDirectory(tmp);
            var zipPath = Path.Combine(tmp, "payload.zip");
            await CreatePayloadZip(zipPath, publishDir, metadata);
            var bytes = await File.ReadAllBytesAsync(zipPath);

            // Inject as RCDATA/PAYLOAD
            if (!EmbedResource(outputPath, "RCDATA", "PAYLOAD", bytes))
                return Result.Failure("Failed to embed payload as Win32 resource");

            try { Directory.Delete(tmp, true); } catch { }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static async Task CreatePayloadZip(string zipPath, string publishDir, InstallerMetadata meta)
    {
        await using var fs = File.Create(zipPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);

        // metadata.json
        var metaEntry = zip.CreateEntry("metadata.json", CompressionLevel.NoCompression);
        await using (var s = metaEntry.Open())
        {
            await JsonSerializer.SerializeAsync(s, meta, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
        }

        // Content/**
        foreach (var file in Directory.EnumerateFiles(publishDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(publishDir, file).Replace('\\', '/');
            var entry = zip.CreateEntry($"Content/{rel}", CompressionLevel.Optimal);
            await using var src = File.OpenRead(file);
            await using var dst = entry.Open();
            await src.CopyToAsync(dst);
        }
    }

    private static bool EmbedResource(string filePath, string type, string name, byte[] data)
    {
        var h = BeginUpdateResource(filePath, false);
        if (h == IntPtr.Zero) return false;
        try
        {
            // 0 = neutral language
            if (!UpdateResource(h, type, name, 0, data, data.Length))
                return false;
            return EndUpdateResource(h, false);
        }
        finally
        {
            // If EndUpdateResource fails, nothing else to do
        }
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr BeginUpdateResource(string pFileName, bool bDeleteExistingResources);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateResource(IntPtr hUpdate, string lpType, string lpName, ushort wLanguage, byte[] lpData, int cbData);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);
}