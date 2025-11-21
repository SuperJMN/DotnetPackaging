using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Exe;

public static class SimpleExePacker
{
    private const string BrandingLogoEntry = "Branding/logo";
    /// <summary>
    /// Builds a self-extracting Windows installer by concatenating:
    ///   [stub.exe][payload.zip][Int64 payloadLength (LE)]["DPACKEXE1"]
    ///
    /// payload.zip contains:
    ///   - metadata.json (InstallerMetadata)
    ///   - Content/** (files from publishDir)
    /// </summary>
    public static async Task<Result> Build(
        string stubPath,
        string publishDir,
        InstallerMetadata metadata,
        Maybe<byte[]> logoBytes,
        string outputPath)
    {
        try
        {
            if (!File.Exists(stubPath))
                return Result.Failure($"Stub not found: {stubPath}");
            if (!Directory.Exists(publishDir))
                return Result.Failure($"Publish directory not found: {publishDir}");

            var tmp = Path.Combine(Path.GetTempPath(), "dp-exe-" + Guid.NewGuid());
            Directory.CreateDirectory(tmp);
            var tempZip = Path.Combine(tmp, "payload.zip");

            await CreatePayloadZip(tempZip, publishDir, metadata, logoBytes);

            using var outFs = File.Create(outputPath);
            using (var stubFs = File.OpenRead(stubPath))
                await stubFs.CopyToAsync(outFs);

            long payloadLen;
            using (var zipFs = File.OpenRead(tempZip))
            {
                payloadLen = zipFs.Length;
                await zipFs.CopyToAsync(outFs);
            }

            // Use heap-allocated buffer to avoid C# 13 ref/stackalloc-in-async requirement
            var len = BitConverter.GetBytes(payloadLen); // little-endian
            await outFs.WriteAsync(len, 0, len.Length);
            var magic = Encoding.ASCII.GetBytes(PayloadFormat.Magic);
            await outFs.WriteAsync(magic, 0, magic.Length);

            try { Directory.Delete(tmp, true); } catch { /* ignore */ }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static async Task CreatePayloadZip(string zipPath, string publishDir, InstallerMetadata meta, Maybe<byte[]> logoBytes)
    {
        using var fs = File.Create(zipPath);
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

        await logoBytes.Match(
            async bytes =>
            {
                var logoEntry = zip.CreateEntry(BrandingLogoEntry, CompressionLevel.NoCompression);
                await using var stream = logoEntry.Open();
                await stream.WriteAsync(bytes, 0, bytes.Length);
            },
            () => Task.CompletedTask);
    }
}
