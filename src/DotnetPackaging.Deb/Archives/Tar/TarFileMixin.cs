using System.Formats.Tar;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using Zafiro.DivineBytes;
using SystemTarEntry = System.Formats.Tar.TarEntry;
using SystemGnuTarEntry = System.Formats.Tar.GnuTarEntry;
using SystemUstarTarEntry = System.Formats.Tar.UstarTarEntry;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class TarFileMixin
{
    private const int TarBlockSize = 512;

    public static IByteSource ToByteSource(this TarFile tarFile)
    {
        var entries = tarFile.Entries.ToList();
        var streamingLength = TryGetUstarStreamingLength(entries);
        if (streamingLength.HasValue)
        {
            return WritingByteSource.FromWriter(
                stream => WriteKnownLengthTarToStream(entries, stream, TarEntryFormat.Ustar),
                streamingLength);
        }

        return ByteSourceMaterializationExtensions.FromTempFileFactory(() => tarFile.ToMaterializedByteSource());
    }

    private static async Task<Result> WriteKnownLengthTarToStream(
        IReadOnlyList<TarEntry> entries,
        Stream stream,
        TarEntryFormat format)
    {
        if (format == TarEntryFormat.Ustar)
        {
            return await WriteUstarToStream(entries, stream).ConfigureAwait(false);
        }

        try
        {
            using var writer = new TarWriter(stream, format, leaveOpen: true);
            foreach (var entry in entries)
            {
                var tarEntry = CreateTarEntry(entry, format);
                if (entry is FileTarEntry file)
                {
                    var content = await file.Content.OpenReadWithLength(".tar-entry").ConfigureAwait(false);
                    if (content.IsFailure)
                    {
                        return Result.Failure($"Could not read tar entry '{entry.Path}': {content.Error}");
                    }

                    using var contentLease = content.Value;
                    tarEntry.DataStream = contentLease.Stream;
                    writer.WriteEntry(tarEntry);
                }
                else
                {
                    writer.WriteEntry(tarEntry);
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Could not write tar archive: {ex.Message}");
        }
    }

    private static async Task<Result> WriteUstarToStream(IReadOnlyList<TarEntry> entries, Stream stream)
    {
        try
        {
            foreach (var entry in entries)
            {
                var header = CreateUstarHeader(entry);
                await stream.WriteAsync(header).ConfigureAwait(false);

                if (entry is not FileTarEntry file)
                {
                    continue;
                }

                var write = await file.Content.WriteTo(stream).ConfigureAwait(false);
                if (write.IsFailure)
                {
                    return Result.Failure($"Could not write tar entry '{entry.Path}': {write.Error}");
                }

                await WritePadding(stream, file.Content.KnownLength().Value).ConfigureAwait(false);
            }

            await stream.WriteAsync(new byte[TarBlockSize * 2]).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Could not write tar archive: {ex.Message}");
        }
    }

    private static async Task<Result<MaterializedByteSourceFile>> ToMaterializedByteSource(this TarFile tarFile)
    {
        var entries = tarFile.Entries.ToList();
        if (FileContentLengthsAreKnown(entries))
        {
            var format = entries.All(CanRepresentAsUstar) ? TarEntryFormat.Ustar : TarEntryFormat.Gnu;
            return await WriteKnownLengthTarToTemp(entries, format).ConfigureAwait(false);
        }

        var materializedEntries = await MaterializeEntries(tarFile.Entries).ConfigureAwait(false);
        if (materializedEntries.IsFailure)
        {
            return Result.Failure<MaterializedByteSourceFile>(materializedEntries.Error);
        }

        try
        {
            return await WriteTarWithFallback(materializedEntries.Value).ConfigureAwait(false);
        }
        finally
        {
            foreach (var entry in materializedEntries.Value)
            {
                entry.Content?.Dispose();
            }
        }
    }

    private static bool FileContentLengthsAreKnown(IEnumerable<TarEntry> entries)
    {
        return entries.OfType<FileTarEntry>().All(file => file.Content.KnownLength().HasValue);
    }

    private static async Task<Result<MaterializedByteSourceFile>> WriteKnownLengthTarToTemp(
        IReadOnlyList<TarEntry> entries,
        TarEntryFormat format)
    {
        var output = MaterializedByteSourceFile.Create(".tar");

        try
        {
            await using var stream = File.Open(output.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var write = await WriteKnownLengthTarToStream(entries, stream, format).ConfigureAwait(false);
            if (write.IsFailure)
            {
                output.Dispose();
                return Result.Failure<MaterializedByteSourceFile>(write.Error);
            }

            return output;
        }
        catch (Exception ex)
        {
            output.Dispose();
            return Result.Failure<MaterializedByteSourceFile>($"Could not write tar archive: {ex.Message}");
        }
    }

    private static Maybe<long> TryGetUstarStreamingLength(IReadOnlyList<TarEntry> entries)
    {
        long total = TarBlockSize * 2;

        foreach (var entry in entries)
        {
            if (!CanRepresentAsUstar(entry))
            {
                return Maybe<long>.None;
            }

            total += TarBlockSize;
            if (entry is not FileTarEntry file)
            {
                continue;
            }

            var contentLength = file.Content.KnownLength();
            if (!contentLength.HasValue)
            {
                return Maybe<long>.None;
            }

            total += PaddedLength(contentLength.Value);
        }

        return Maybe.From(total);
    }

    private static long PaddedLength(long length)
    {
        var remainder = length % TarBlockSize;
        return remainder == 0 ? length : length + TarBlockSize - remainder;
    }

    private static async Task WritePadding(Stream stream, long length)
    {
        var padding = PaddedLength(length) - length;
        if (padding > 0)
        {
            await stream.WriteAsync(new byte[(int)padding]).ConfigureAwait(false);
        }
    }

    private static byte[] CreateUstarHeader(TarEntry entry)
    {
        var header = new byte[TarBlockSize];
        var isDirectory = entry is DirectoryTarEntry;
        var normalizedPath = Normalize(entry.Path, isDirectory);
        if (!TrySplitUstarPath(normalizedPath, out var prefix, out var name))
        {
            throw new InvalidDataException($"Path '{normalizedPath}' cannot be represented as UStar.");
        }

        WriteText(header, 0, 100, name);
        WriteOctal(header, 100, 8, entry.Properties.Mode);
        WriteOctal(header, 108, 8, entry.Properties.OwnerId);
        WriteOctal(header, 116, 8, entry.Properties.GroupId);
        WriteOctal(header, 124, 12, entry is FileTarEntry file ? file.Content.KnownLength().Value : 0);
        WriteOctal(header, 136, 12, entry.Properties.LastModification.ToUnixTimeSeconds());

        for (var i = 148; i < 156; i++)
        {
            header[i] = (byte)' ';
        }

        header[156] = isDirectory ? (byte)'5' : (byte)'0';
        WriteText(header, 257, 6, "ustar");
        WriteText(header, 263, 2, "00");
        WriteText(header, 265, 32, entry.Properties.OwnerUsername);
        WriteText(header, 297, 32, entry.Properties.GroupName);
        WriteText(header, 345, 155, prefix);

        var checksum = header.Sum(x => (int)x);
        var checksumText = Convert.ToString(checksum, 8).PadLeft(6, '0');
        WriteText(header, 148, 6, checksumText);
        header[154] = 0;
        header[155] = (byte)' ';

        return header;
    }

    private static void WriteText(byte[] buffer, int offset, int length, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > length)
        {
            throw new InvalidDataException($"Value '{value}' is too long for a UStar field of {length} bytes.");
        }

        Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
    }

    private static void WriteOctal(byte[] buffer, int offset, int length, long value)
    {
        var text = Convert.ToString(value, 8);
        if (text.Length > length - 1)
        {
            throw new InvalidDataException($"Value '{value}' is too large for a UStar octal field of {length} bytes.");
        }

        WriteText(buffer, offset, length - 1, text.PadLeft(length - 1, '0'));
        buffer[offset + length - 1] = 0;
    }

    private static bool CanRepresentAsUstar(TarEntry entry)
    {
        var name = Normalize(entry.Path, entry is DirectoryTarEntry);
        return CanSplitUstarPath(name)
               && Encoding.UTF8.GetByteCount(entry.Properties.OwnerUsername) <= 32
               && Encoding.UTF8.GetByteCount(entry.Properties.GroupName) <= 32;
    }

    private static bool CanSplitUstarPath(string path)
    {
        return TrySplitUstarPath(path, out _, out _);
    }

    private static bool TrySplitUstarPath(string path, out string prefix, out string name)
    {
        prefix = string.Empty;
        name = path;

        if (Encoding.UTF8.GetByteCount(path) <= 100)
        {
            return true;
        }

        for (var i = path.Length - 1; i > 0; i--)
        {
            if (path[i] != '/')
            {
                continue;
            }

            var candidatePrefix = path[..i];
            var candidateName = path[(i + 1)..];
            if (candidateName.Length == 0)
            {
                continue;
            }

            if (Encoding.UTF8.GetByteCount(candidatePrefix) <= 155 && Encoding.UTF8.GetByteCount(candidateName) <= 100)
            {
                prefix = candidatePrefix;
                name = candidateName;
                return true;
            }
        }

        return false;
    }

    private static async Task<Result<MaterializedByteSourceFile>> WriteTarWithFallback(IReadOnlyList<MaterializedTarEntry> entries)
    {
        // Prefer UStar for maximum compatibility (Eddy), fallback to GNU for very long paths.
        var ustar = await WriteTar(entries, TarEntryFormat.Ustar).ConfigureAwait(false);
        if (ustar.Outcome.IsSuccess || !ustar.CanFallback)
        {
            return ustar.Outcome;
        }

        return (await WriteTar(entries, TarEntryFormat.Gnu).ConfigureAwait(false)).Outcome;
    }

    private static async Task<Result<IReadOnlyList<MaterializedTarEntry>>> MaterializeEntries(IEnumerable<TarEntry> entries)
    {
        var materialized = new List<MaterializedTarEntry>();

        foreach (var entry in entries)
        {
            if (entry is not FileTarEntry file)
            {
                materialized.Add(new MaterializedTarEntry(entry, null));
                continue;
            }

            var contentFile = await file.Content.ToTempFile(".tar-entry").ConfigureAwait(false);
            if (contentFile.IsFailure)
            {
                foreach (var materializedEntry in materialized)
                {
                    materializedEntry.Content?.Dispose();
                }

                return Result.Failure<IReadOnlyList<MaterializedTarEntry>>(
                    $"Could not read tar entry '{entry.Path}': {contentFile.Error}");
            }

            materialized.Add(new MaterializedTarEntry(entry, contentFile.Value));
        }

        return materialized;
    }

    private static async Task<TarWriteAttempt> WriteTar(IReadOnlyList<MaterializedTarEntry> entries, TarEntryFormat format)
    {
        var output = MaterializedByteSourceFile.Create(".tar");

        try
        {
            await using var stream = File.Open(output.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using (var writer = new TarWriter(stream, format, leaveOpen: true))
            {
                foreach (var materialized in entries)
                {
                    var entry = materialized.Entry;
                    var tarEntry = CreateTarEntry(entry, format);
                    if (entry is FileTarEntry)
                    {
                        // TarWriter is synchronous and asks DataStream.Length. Use a real
                        // seekable file instead of blocking an Rx-backed stream in place.
                        using var contentStream = materialized.Content!.OpenRead();
                        tarEntry.DataStream = contentStream;
                        writer.WriteEntry(tarEntry);
                    }
                    else
                    {
                        writer.WriteEntry(tarEntry);
                    }
                }
            }

            return TarWriteAttempt.Success(output);
        }
        catch (ArgumentException ex) when (format == TarEntryFormat.Ustar)
        {
            output.Dispose();
            return TarWriteAttempt.Fallback(ex.Message);
        }
        catch (InvalidDataException ex) when (format == TarEntryFormat.Ustar)
        {
            output.Dispose();
            return TarWriteAttempt.Fallback(ex.Message);
        }
        catch (Exception ex)
        {
            output.Dispose();
            return TarWriteAttempt.Failure($"Could not write tar archive: {ex.Message}");
        }
    }

    private static SystemTarEntry CreateTarEntry(TarEntry entry, TarEntryFormat format)
    {
        var isDirectory = entry is DirectoryTarEntry;
        var tarEntryType = isDirectory ? TarEntryType.Directory : TarEntryType.RegularFile;
        var name = Normalize(entry.Path, isDirectory);

        SystemTarEntry tarEntry = format switch
        {
            TarEntryFormat.Ustar => new SystemUstarTarEntry(tarEntryType, name),
            TarEntryFormat.Gnu => new SystemGnuTarEntry(tarEntryType, name),
            _ => throw new NotSupportedException($"Tar format '{format}' is not supported")
        };

        tarEntry.Mode = (UnixFileMode)entry.Properties.Mode;
        tarEntry.ModificationTime = entry.Properties.LastModification.UtcDateTime;
        tarEntry.Gid = entry.Properties.GroupId;
        tarEntry.Uid = entry.Properties.OwnerId;
        if (tarEntry is PosixTarEntry posix)
        {
            posix.UserName = entry.Properties.OwnerUsername;
            posix.GroupName = entry.Properties.GroupName;
        }
        return tarEntry;
    }

    private static string Normalize(string path, bool isDirectory)
    {
        var normalized = path.Replace("\\", "/", StringComparison.Ordinal);
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        normalized = normalized.TrimStart('/');

        if (!normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = $"./{normalized.TrimStart('.')}";
        }

        if (string.Equals(normalized, "./", StringComparison.Ordinal))
        {
            normalized = "./";
        }

        if (isDirectory && !normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized += "/";
        }

        return normalized;
    }

    private sealed record MaterializedTarEntry(TarEntry Entry, MaterializedByteSourceFile? Content);

    private sealed record TarWriteAttempt(Result<MaterializedByteSourceFile> Outcome, bool CanFallback)
    {
        public static TarWriteAttempt Success(MaterializedByteSourceFile file) => new(Result.Success(file), false);
        public static TarWriteAttempt Failure(string error) => new(Result.Failure<MaterializedByteSourceFile>(error), false);
        public static TarWriteAttempt Fallback(string error) => new(Result.Failure<MaterializedByteSourceFile>(error), true);
    }
}
