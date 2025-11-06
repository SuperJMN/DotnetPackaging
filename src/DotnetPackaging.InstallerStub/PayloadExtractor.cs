using System.Text;
using System.Text.Json;
using System.IO.Compression;

namespace DotnetPackaging.InstallerStub;

internal static class PayloadExtractor
{
    private const string Magic = "DPACKEXE1"; // must match packer

    public static (string contentDir, InstallerMetadata meta) Extract()
    {
        var self = Environment.ProcessPath!;
        using var fs = File.OpenRead(self);
        var magicBytes = Encoding.ASCII.GetBytes(Magic);

        fs.Seek(-magicBytes.Length, SeekOrigin.End);
        var bufMagic = new byte[magicBytes.Length];
        _ = fs.Read(bufMagic, 0, bufMagic.Length);
        if (!bufMagic.SequenceEqual(magicBytes)) throw new InvalidOperationException("Invalid payload marker");

        fs.Seek(-(magicBytes.Length + 8), SeekOrigin.End);
        Span<byte> lenBytes = stackalloc byte[8];
        _ = fs.Read(lenBytes);
        long payloadLen = BitConverter.ToInt64(lenBytes);

        long payloadStart = fs.Length - payloadLen - magicBytes.Length - 8;
        fs.Position = payloadStart;

        var tempDir = Directory.CreateTempSubdirectory("dp-inst-_").FullName;
        var zipPath = Path.Combine(tempDir, "payload.zip");
        using (var z = File.Create(zipPath))
        {
            CopyFixed(fs, z, payloadLen);
        }

        // metadata
        InstallerMetadata meta;
        using (var za = ZipFile.OpenRead(zipPath))
        {
            var metaEntry = za.GetEntry("metadata.json") ?? throw new InvalidOperationException("metadata.json missing");
            using var s = metaEntry.Open();
            meta = JsonSerializer.Deserialize<InstallerMetadata>(s)!;
        }

        var contentOut = Path.Combine(tempDir, "Content");
        ZipFile.ExtractToDirectory(zipPath, tempDir);
        return (contentOut, meta);
    }

    private static void CopyFixed(Stream src, Stream dst, long bytes)
    {
        var buffer = new byte[81920];
        long remaining = bytes;
        while (remaining > 0)
        {
            int toRead = (int)Math.Min(buffer.Length, remaining);
            int n = src.Read(buffer, 0, toRead);
            if (n <= 0) throw new EndOfStreamException();
            dst.Write(buffer, 0, n);
            remaining -= n;
        }
    }
}

internal sealed record InstallerMetadata(
    string AppId,
    string ApplicationName,
    string Version,
    string Vendor,
    string? Description = null,
    string? ExecutableName = null);
