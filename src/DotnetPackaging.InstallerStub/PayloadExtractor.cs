using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace DotnetPackaging.InstallerStub;

internal static class PayloadExtractor
{
    private const string Magic = "DPACKEXE1"; // legacy footer (concat mode)

    public static (string contentDir, InstallerMetadata meta) Extract()
    {
        // 1) Try managed embedded resource first (PublishSingleFile-friendly)
        var managed = TryExtractFromManagedResource();
        if (managed is not null) return managed.Value;

        // 2) Try Win32 resource (RCDATA)
        var fromRes = TryExtractFromResource();
        if (fromRes is not null) return fromRes.Value;

        // 3) Fallback: appended payload footer (legacy)
        var self = Environment.ProcessPath!;
        using var fs = File.OpenRead(self);
        var magicBytes = Encoding.ASCII.GetBytes(Magic);

        fs.Seek(-magicBytes.Length, SeekOrigin.End);
        var bufMagic = new byte[magicBytes.Length];
        _ = fs.Read(bufMagic, 0, bufMagic.Length);
        if (!bufMagic.SequenceEqual(magicBytes)) throw new InvalidOperationException("Installer payload not found");

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

        return ReadZip(zipPath, tempDir);
    }

    private static (string contentDir, InstallerMetadata meta) ReadZip(string zipPath, string tempDir)
    {
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

    private static (string contentDir, InstallerMetadata meta)? TryExtractFromManagedResource()
    {
        try
        {
            var asm = typeof(PayloadExtractor).Assembly;
            // Try exact logical name first, then fall back to any resource containing PAYLOAD
            var names = asm.GetManifestResourceNames();
            var resName = names.FirstOrDefault(n => string.Equals(n, "PAYLOAD", StringComparison.OrdinalIgnoreCase))
                          ?? names.FirstOrDefault(n => n.Contains("PAYLOAD", StringComparison.OrdinalIgnoreCase))
                          ?? names.FirstOrDefault(n => n.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase));
            if (resName is null) return null;
            using var s = asm.GetManifestResourceStream(resName);
            if (s == null) return null;
            var tempDir = Directory.CreateTempSubdirectory("dp-inst-_").FullName;
            var zipPath = Path.Combine(tempDir, "payload.zip");
            using (var fs = File.Create(zipPath))
            {
                s.CopyTo(fs);
            }
            return ReadZip(zipPath, tempDir);
        }
        catch
        {
            return null;
        }
    }

    private static (string contentDir, InstallerMetadata meta)? TryExtractFromResource()
    {
        try
        {
            var hModule = GetModuleHandle(null);
            var hResInfo = FindResource(hModule, "PAYLOAD", "RCDATA");
            if (hResInfo == IntPtr.Zero) return null;
            var size = SizeofResource(hModule, hResInfo);
            if (size == 0) return null;
            var hResData = LoadResource(hModule, hResInfo);
            if (hResData == IntPtr.Zero) return null;
            var pData = LockResource(hResData);
            if (pData == IntPtr.Zero) return null;

            var bytes = new byte[size];
            Marshal.Copy(pData, bytes, 0, (int)size);

            var tempDir = Directory.CreateTempSubdirectory("dp-inst-_").FullName;
            var zipPath = Path.Combine(tempDir, "payload.zip");
            File.WriteAllBytes(zipPath, bytes);
            return ReadZip(zipPath, tempDir);
        }
        catch
        {
            return null;
        }
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

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindResource(IntPtr hModule, string lpName, string lpType);

    [DllImport("kernel32", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32", SetLastError = true)]
    private static extern IntPtr LockResource(IntPtr hResData);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}