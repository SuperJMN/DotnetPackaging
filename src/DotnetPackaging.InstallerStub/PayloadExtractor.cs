using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Linq;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.InstallerStub;

internal static class PayloadExtractor
{
    private const string Magic = "DPACKEXE1"; // legacy footer (concat mode)

    public static Result<PayloadPreparation> Prepare()
    {
        var attempts = new List<Func<Result<PayloadPreparation>>>
        {
            AttemptFrom(TryExtractFromManagedResource, "Managed payload not found"),
            AttemptFrom(TryExtractFromResource, "Win32 resource payload not found"),
            () => TryExtractFromAppendedPayload().Bind(CreatePreparation)
        };

        var errors = new List<string>();

        foreach (var attempt in attempts)
        {
            var result = attempt();
            if (result.IsSuccess)
            {
                return result;
            }

            errors.Add(result.Error);
        }

        return Result.Failure<PayloadPreparation>(string.Join(Environment.NewLine, errors.Distinct()));
    }

    private static Func<Result<PayloadPreparation>> AttemptFrom(Func<Maybe<PayloadLocation>> extractor, string missingMessage)
    {
        return () => extractor()
            .ToResult(missingMessage)
            .Bind(CreatePreparation);
    }

    private static Result<PayloadPreparation> CreatePreparation(PayloadLocation location)
    {
        return ReadMetadata(location.ZipPath)
            .Map(metadata => new PayloadPreparation(metadata, () => ExtractPayload(location)));
    }

    private static Result<InstallerMetadata> ReadMetadata(string zipPath)
    {
        return Result.Try(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var metaEntry = archive.GetEntry("metadata.json") ?? throw new InvalidOperationException("metadata.json missing");
            using var stream = metaEntry.Open();
            return JsonSerializer.Deserialize<InstallerMetadata>(stream)!;
        }, ex => $"Error reading payload metadata: {ex.Message}");
    }

    private static Result<string> ExtractPayload(PayloadLocation location)
    {
        return Result.Try(() =>
        {
            var contentOut = Path.Combine(location.TempDir, "Content");
            if (!Directory.Exists(contentOut) || !Directory.EnumerateFileSystemEntries(contentOut).Any())
            {
                ZipFile.ExtractToDirectory(location.ZipPath, location.TempDir, true);
            }

            return contentOut;
        }, ex => $"Error decompressing payload: {ex.Message}");
    }

    private static Maybe<PayloadLocation> TryExtractFromManagedResource()
    {
        try
        {
            var asm = typeof(PayloadExtractor).Assembly;
            // Try exact logical name first, then fall back to any resource containing PAYLOAD
            var names = asm.GetManifestResourceNames();
            var resName = names.FirstOrDefault(n => string.Equals(n, "PAYLOAD", StringComparison.OrdinalIgnoreCase))
                          ?? names.FirstOrDefault(n => n.Contains("PAYLOAD", StringComparison.OrdinalIgnoreCase))
                          ?? names.FirstOrDefault(n => n.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase));
            if (resName is null) return Maybe<PayloadLocation>.None;
            using var s = asm.GetManifestResourceStream(resName);
            if (s == null) return Maybe<PayloadLocation>.None;
            var tempDir = Directory.CreateTempSubdirectory("dp-inst-_").FullName;
            var zipPath = Path.Combine(tempDir, "payload.zip");
            using (var fs = File.Create(zipPath))
            {
                s.CopyTo(fs);
            }
            return new PayloadLocation(tempDir, zipPath);
        }
        catch
        {
            return Maybe<PayloadLocation>.None;
        }
    }

    private static Maybe<PayloadLocation> TryExtractFromResource()
    {
        try
        {
            var hModule = GetModuleHandle(null);
            var hResInfo = FindResource(hModule, "PAYLOAD", "RCDATA");
            if (hResInfo == IntPtr.Zero) return Maybe<PayloadLocation>.None;
            var size = SizeofResource(hModule, hResInfo);
            if (size == 0) return Maybe<PayloadLocation>.None;
            var hResData = LoadResource(hModule, hResInfo);
            if (hResData == IntPtr.Zero) return Maybe<PayloadLocation>.None;
            var pData = LockResource(hResData);
            if (pData == IntPtr.Zero) return Maybe<PayloadLocation>.None;

            var bytes = new byte[size];
            Marshal.Copy(pData, bytes, 0, (int)size);

            var tempDir = Directory.CreateTempSubdirectory("dp-inst-_").FullName;
            var zipPath = Path.Combine(tempDir, "payload.zip");
            File.WriteAllBytes(zipPath, bytes);
            return new PayloadLocation(tempDir, zipPath);
        }
        catch
        {
            return Maybe<PayloadLocation>.None;
        }
    }

    private static Result<PayloadLocation> TryExtractFromAppendedPayload()
    {
        return Result.Try(() =>
        {
            var self = Environment.ProcessPath!;
            using var fs = File.OpenRead(self);
            var magicBytes = Encoding.ASCII.GetBytes(Magic);

            fs.Seek(-magicBytes.Length, SeekOrigin.End);
            var bufMagic = new byte[magicBytes.Length];
            _ = fs.Read(bufMagic, 0, bufMagic.Length);
            if (!bufMagic.SequenceEqual(magicBytes))
            {
                throw new InvalidOperationException("Installer payload not found");
            }

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

            return new PayloadLocation(tempDir, zipPath);
        }, ex => ex.Message);
    }

    private sealed record PayloadLocation(string TempDir, string ZipPath);

    internal sealed class PayloadPreparation
    {
        private readonly Lazy<Result<string>> extraction;

        public PayloadPreparation(InstallerMetadata metadata, Func<Result<string>> extractContent)
        {
            Metadata = metadata;
            extraction = new Lazy<Result<string>>(() => extractContent());
        }

        public InstallerMetadata Metadata { get; }

        public Result<string> ExtractContent() => extraction.Value;
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