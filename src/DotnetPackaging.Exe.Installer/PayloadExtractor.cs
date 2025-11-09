using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using Zafiro.ProgressReporting;

namespace DotnetPackaging.Exe.Installer;

internal static class PayloadExtractor
{
    private const string Magic = "DPACKEXE1"; // legacy footer (concat mode)

    public static Result<InstallerPayload> LoadPayload()
    {
        var attempts = new List<Func<Result<InstallerPayload>>>
        {
            AttemptLoadPayloadFrom(TryExtractFromManagedResource, "Managed payload not found"),
            AttemptLoadPayloadFrom(TryExtractFromResource, "Win32 resource payload not found"),
            () => TryExtractFromAppendedPayload().Bind(CreatePayload)
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

#if DEBUG
        var debugPayload = CreateDebugPayload();
        if (debugPayload.IsSuccess)
        {
            return debugPayload;
        }

        errors.Add(debugPayload.Error);
#endif

        return Result.Failure<InstallerPayload>(string.Join(Environment.NewLine, errors.Distinct()));
    }

    public static Result CopyContentTo(InstallerPayload payload, string targetDirectory, IObserver<Progress>? progressObserver)
    {
        return Result.Try(() =>
        {
            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(targetDirectory, true);
            }

            Directory.CreateDirectory(targetDirectory);

            using var stream = payload.Content.ToStreamSeekable();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

            var contentEntries = archive.Entries
                .Where(IsContentEntry)
                .ToList();

            var fileEntries = contentEntries.Where(entry => !IsDirectoryEntry(entry));
            long totalBytes = fileEntries.Sum(entry => entry.Length);
            long safeTotal = totalBytes == 0 ? 1 : totalBytes;
            long copiedBytes = 0;
            var targetRoot = System.IO.Path.GetFullPath(targetDirectory);

            foreach (var entry in contentEntries)
            {
                if (!TryGetRelativeContentPath(entry.FullName, out var relativePath))
                {
                    continue;
                }

                var destinationPath = System.IO.Path.Combine(targetDirectory, relativePath);
                var destinationFullPath = System.IO.Path.GetFullPath(destinationPath);
                if (!destinationFullPath.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Payload entry '{entry.FullName}' is outside of the installation directory");
                }

                if (IsDirectoryEntry(entry))
                {
                    Directory.CreateDirectory(destinationFullPath);
                    continue;
                }

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destinationFullPath)!);
                using var entryStream = entry.Open();
                using var fileStream = File.Create(destinationFullPath);
                entryStream.CopyTo(fileStream);

                copiedBytes += entry.Length;
                var relativeProgress = new Absolute(safeTotal, copiedBytes);
                progressObserver?.OnNext(relativeProgress);
            }
        }, ex => $"Error extracting payload content: {ex.Message}");
    }

    private static Func<Result<InstallerPayload>> AttemptLoadPayloadFrom(Func<Maybe<PayloadLocation>> extractor, string missingMessage)
    {
        return () => extractor()
            .ToResult(missingMessage)
            .Bind(CreatePayload);
    }

    private static Func<Result<InstallerPayload>> AttemptLoadPayloadFrom(Func<Result<PayloadLocation>> extractor)
    {
        return () => extractor().Bind(CreatePayload);
    }

    private static Result<InstallerPayload> CreatePayload(PayloadLocation location)
    {
        var bytesResult = Result.Try(() => File.ReadAllBytes(location.ZipPath), ex => $"Error loading payload: {ex.Message}");

        var result = bytesResult
            .Bind(bytes => ReadMetadata(bytes)
                .Map(metadata => new InstallerPayload(
                    metadata,
                    ByteSource.FromBytes(bytes),
                    location.TempDir)));

        TryDeleteFile(location.ZipPath);

        if (result.IsFailure)
        {
            TryDeleteDirectory(location.TempDir);
        }

        return result;
    }

    private static Result<InstallerMetadata> ReadMetadata(byte[] zipBytes)
    {
        return Result.Try(() =>
        {
            using var stream = new MemoryStream(zipBytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var metaEntry = archive.GetEntry("metadata.json") ?? throw new InvalidOperationException("metadata.json missing");
            using var entryStream = metaEntry.Open();
            return JsonSerializer.Deserialize<InstallerMetadata>(entryStream)!;
        }, ex => $"Error reading payload metadata: {ex.Message}");
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
            var zipPath = System.IO.Path.Combine(tempDir, "payload.zip");
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
            var zipPath = System.IO.Path.Combine(tempDir, "payload.zip");
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
            var zipPath = System.IO.Path.Combine(tempDir, "payload.zip");
            using (var z = File.Create(zipPath))
            {
                CopyFixed(fs, z, payloadLen);
            }

            return new PayloadLocation(tempDir, zipPath);
        }, ex => ex.Message);
    }

    private sealed record PayloadLocation(string TempDir, string ZipPath);

    private static void TryDeleteDirectory(string tempDir)
    {
        if (string.IsNullOrWhiteSpace(tempDir))
        {
            return;
        }

        try
        {
            Directory.Delete(tempDir, true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore clean-up failures
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

    private static bool IsContentEntry(ZipArchiveEntry entry)
    {
        return IsContentEntry(entry.FullName);
    }

    private static bool IsContentEntry(string entryName)
    {
        var normalized = entryName.Replace('\\', '/');
        return normalized.StartsWith("Content/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetRelativeContentPath(string entryFullName, out string relativePath)
    {
        var normalized = entryFullName.Replace('\\', '/');
        if (!normalized.StartsWith("Content/", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = string.Empty;
            return false;
        }

        var slice = normalized.Substring("Content/".Length);
        if (string.IsNullOrWhiteSpace(slice))
        {
            relativePath = string.Empty;
            return false;
        }

        relativePath = slice.Replace('/', System.IO.Path.DirectorySeparatorChar);
        return true;
    }

    private static bool IsDirectoryEntry(ZipArchiveEntry entry)
    {
        return string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith("/", StringComparison.Ordinal);
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

#if DEBUG
    private static Result<InstallerPayload> CreateDebugPayload()
    {
        return Result.Try(() =>
        {
            var metadata = new InstallerMetadata("debug.app", "Debug Application", "1.0.0", "DotnetPackaging");
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var metadataEntry = archive.CreateEntry("metadata.json");
                using (var metadataStream = metadataEntry.Open())
                {
                    JsonSerializer.Serialize(metadataStream, metadata);
                }

                var helloEntry = archive.CreateEntry("Content/Hello.txt");
                using var writer = new StreamWriter(helloEntry.Open(), Encoding.UTF8, leaveOpen: false);
                writer.Write("This is a text.");
            }

            var tempDir = Directory.CreateTempSubdirectory("dp-inst-debug-").FullName;
            return new InstallerPayload(metadata, ByteSource.FromBytes(ms.ToArray()), tempDir);
        }, ex => $"Failed to create debug payload: {ex.Message}");
    }
#endif
}

public sealed record InstallerPayload(InstallerMetadata Metadata, IByteSource Content, string WorkingDirectory);
