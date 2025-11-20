using System.Diagnostics;
using System.IO.Compression;
using Serilog;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using Zafiro.ProgressReporting;

namespace DotnetPackaging.Exe.Installer.Core;

internal static class PayloadExtractor
{
    private const string Magic = "DPACKEXE1"; // legacy footer (concat mode)

    public static Result<InstallerPayload> LoadPayload()
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("DP_ATTACH_DEBUGGER") == "1")
        {
            try { Debugger.Launch(); } catch { /* ignore */ }
        }
#endif
        var attempts = new List<Func<Result<InstallerPayload>>>
        {
            AttemptLoadPayloadFrom(TryExtractFromManagedResource, "Managed payload not found"),
            () => TryExtractFromAppendedPayload().Bind(CreatePayload),
            AttemptLoadPayloadFrom(TryExtractFromDisk, "Payload not found on disk (Uninstall mode)")
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

    public static Maybe<long> GetAppendedPayloadStart(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            var searchWindow = 4096;
            if (fs.Length < searchWindow) searchWindow = (int)fs.Length;

            var buffer = new byte[searchWindow];
            fs.Seek(-searchWindow, SeekOrigin.End);
            var bytesRead = fs.Read(buffer, 0, searchWindow);

            var magicBytes = Encoding.ASCII.GetBytes(Magic);
            var magicPos = FindPattern(buffer, magicBytes);

            if (magicPos == -1)
            {
                return Maybe<long>.None;
            }

            var footerStartInFile = fs.Length - searchWindow + magicPos;
            var lengthPos = footerStartInFile - 8;

            fs.Seek(lengthPos, SeekOrigin.Begin);
            Span<byte> lenBytes = stackalloc byte[8];
            _ = fs.Read(lenBytes);
            long payloadLen = BitConverter.ToInt64(lenBytes);

            long payloadStart = lengthPos - payloadLen;
            if (payloadStart < 0)
            {
                return Maybe<long>.None;
            }

            return payloadStart;
        }
        catch
        {
            return Maybe<long>.None;
        }
    }

    private static Maybe<PayloadLocation> TryExtractFromDisk()
    {
        var dir = System.IO.Path.GetDirectoryName(Environment.ProcessPath);
        if (dir == null) return Maybe<PayloadLocation>.None;

        var metadataPath = System.IO.Path.Combine(dir, "metadata.json");
        if (!File.Exists(metadataPath)) return Maybe<PayloadLocation>.None;

        try
        {
            var tempDir = Directory.CreateTempSubdirectory("dp-inst-uninstall-").FullName;
            var zipPath = System.IO.Path.Combine(tempDir, "payload.zip");

            using (var fs = File.Create(zipPath))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
            {
                var entry = archive.CreateEntry("metadata.json");
                using var es = entry.Open();
                using var input = File.OpenRead(metadataPath);
                input.CopyTo(es);
            }

            return new PayloadLocation(tempDir, zipPath);
        }
        catch
        {
            return Maybe<PayloadLocation>.None;
        }
    }

    public static Result CopyContentTo(InstallerPayload payload, string targetDirectory, IObserver<Progress>? progressObserver)
    {
        return Result.Try(() =>
        {
            if (Directory.Exists(targetDirectory))
            {
                Log.Information("Target directory exists, attempting to remove or rename: {Directory}", targetDirectory);
                TryRemoveOrRenameDirectory(targetDirectory);
            }

            Directory.CreateDirectory(targetDirectory);

            using var stream = payload.Content.ToStreamSeekable();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

            var contentEntries = archive.Entries
                .Where(IsContentEntry)
                .ToList();

            try 
            { 
                File.WriteAllText(
                    System.IO.Path.Combine(targetDirectory, "install_debug.log"), 
                    $"Found {contentEntries.Count} entries to extract from {archive.Entries.Count} total entries.\nTarget: {targetDirectory}\n"); 
            } 
            catch { }

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
                
                // Debug log extraction
                Log.Debug("Extracting: {Entry} -> {Destination}", entry.FullName, destinationFullPath);

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
                var relativeProgress = new Absolute(copiedBytes, safeTotal);
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
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<InstallerMetadata>(entryStream, opts)!;
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


    private static Result<PayloadLocation> TryExtractFromAppendedPayload()
    {
        return Result.Try(() =>
        {
            var self = Environment.ProcessPath!;
            try { File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-payload-debug.txt"), $"Reading payload from: {self}\n"); } catch { }
            
            using var fs = File.OpenRead(self);
            try { File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-payload-debug.txt"), $"File length: {fs.Length}\n"); } catch { }

            // Search for magic bytes in the last 1KB of the file to account for potential signing or other padding
            // The magic footer is "DPACKEXE1" (9 bytes) + Length (8 bytes) = 17 bytes minimum
            
            var searchWindow = 4096;
            if (fs.Length < searchWindow) searchWindow = (int)fs.Length;
            
            var buffer = new byte[searchWindow];
            fs.Seek(-searchWindow, SeekOrigin.End);
            var bytesRead = fs.Read(buffer, 0, searchWindow);
            
            var magicBytes = Encoding.ASCII.GetBytes(Magic);
            var magicPos = FindPattern(buffer, magicBytes);
            
            try { File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-payload-debug.txt"), $"Magic bytes found at offset: {magicPos}\n"); } catch { }
            
            if (magicPos == -1)
            {
                throw new InvalidOperationException("Installer payload not found (Magic footer missing)");
            }
            
            // magicPos is the index in the buffer where "DPACKEXE1" starts.
            // The length (8 bytes) precedes the magic.
            // Layout: [Payload] [Length (8 bytes)] [Magic (9 bytes)]
            
            var footerStartInFile = fs.Length - searchWindow + magicPos;
            var lengthPos = footerStartInFile - 8;
            
            fs.Seek(lengthPos, SeekOrigin.Begin);
            Span<byte> lenBytes = stackalloc byte[8];
            _ = fs.Read(lenBytes);
            long payloadLen = BitConverter.ToInt64(lenBytes);
            
            long payloadStart = lengthPos - payloadLen;
            if (payloadStart < 0)
            {
                 throw new InvalidOperationException($"Invalid payload length/offset calculated (Len: {payloadLen}, Start: {payloadStart})");
            }

            fs.Position = payloadStart;

            var tempDir = Directory.CreateTempSubdirectory("dp-inst-").FullName;
            var zipPath = System.IO.Path.Combine(tempDir, "payload.zip");
            using (var z = File.Create(zipPath))
            {
                CopyFixed(fs, z, payloadLen);
            }

            return new PayloadLocation(tempDir, zipPath);
        }, ex => ex.Message);
    }
    
    private static int FindPattern(byte[] data, byte[] pattern)
    {
        for (int i = data.Length - pattern.Length; i >= 0; i--)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
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

    private static void TryRemoveOrRenameDirectory(string directory)
    {
        // First try to delete normally
        try
        {
            Directory.Delete(directory, true);
            Log.Information("Successfully deleted directory: {Directory}", directory);
            return;
        }
        catch (Exception ex)
        {
            Log.Warning("Cannot delete directory (may be locked): {Error}. Attempting to rename...", ex.Message);
        }

        // If delete fails, try to rename it
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var renamed = $"{directory}.old-{timestamp}-{attempt}";
                Directory.Move(directory, renamed);
                Log.Information("Directory renamed to: {RenamedDirectory}", renamed);
                
                // Schedule deletion on reboot
                ScheduleDirectoryDeletionOnReboot(renamed);
                return;
            }
            catch (Exception ex) when (attempt < 5)
            {
                Log.Warning("Attempt {Attempt}/5 to rename directory failed: {Error}", attempt, ex.Message);
                Thread.Sleep(200);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to rename directory after 5 attempts");
                throw new InvalidOperationException(
                    $"Cannot remove or rename existing installation directory '{directory}'. " +
                    $"Please close any programs using files in this directory (including Windows Explorer) and try again.", 
                    ex);
            }
        }
    }

    private static void ScheduleDirectoryDeletionOnReboot(string directory)
    {
        try
        {
            // Mark for deletion on next reboot using MoveFileEx
            // This is a best-effort operation
            Log.Information("Scheduling directory for deletion on reboot: {Directory}", directory);
            
            // Recursively mark all files and subdirectories
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    if (!MoveFileEx(file, null, MoveFileFlags.DelayUntilReboot))
                    {
                        Log.Debug("Could not schedule file for deletion: {File}", file);
                    }
                }
                catch { /* best effort */ }
            }
            
            // Mark the directory itself
            if (!MoveFileEx(directory, null, MoveFileFlags.DelayUntilReboot))
            {
                Log.Debug("Could not schedule directory for deletion: {Directory}", directory);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to schedule deletion on reboot for: {Directory}", directory);
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, MoveFileFlags dwFlags);

    [Flags]
    private enum MoveFileFlags
    {
        DelayUntilReboot = 0x4
    }

    private static void TryDeleteDirectoryWithRetry(string directory, int maxAttempts = 3)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    Log.Information("Directory already deleted: {Directory}", directory);
                    return;
                }

                // First try to remove read-only attributes from all files
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var attrs = File.GetAttributes(file);
                        if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
                        }
                    }
                    catch { /* Best effort */ }
                }

                Directory.Delete(directory, true);
                Log.Information("Successfully deleted directory: {Directory}", directory);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                Log.Warning("Attempt {Attempt}/{MaxAttempts} to delete directory failed: {Error}. Retrying...", 
                    attempt, maxAttempts, ex.Message);
                Thread.Sleep(500 * attempt); // Exponential backoff: 500ms, 1000ms
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete directory after {Attempts} attempts: {Directory}", 
                    maxAttempts, directory);
                throw new InvalidOperationException(
                    $"Cannot delete existing installation directory. Please close any programs that may be using files in '{directory}' and try again.", 
                    ex);
            }
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
