using System.Formats.Tar;
using Zafiro.DivineBytes;
using SystemTarEntry = System.Formats.Tar.TarEntry;
using SystemGnuTarEntry = System.Formats.Tar.GnuTarEntry;
using SystemUstarTarEntry = System.Formats.Tar.UstarTarEntry;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class TarFileMixin
{
    public static IByteSource ToByteSource(this TarFile tarFile)
    {
        // Prefer UStar for maximum compatibility (Eddy), fallback to GNU for very long paths.
        try
        {
            return WriteTar(tarFile, TarEntryFormat.Ustar);
        }
        catch (ArgumentException) { }
        catch (InvalidDataException) { }

        return WriteTar(tarFile, TarEntryFormat.Gnu);
    }

    private static IByteSource WriteTar(TarFile tarFile, TarEntryFormat format)
    {
        using var stream = new MemoryStream();
        using (var writer = new TarWriter(stream, format, leaveOpen: true))
        {
            foreach (var entry in tarFile.Entries)
            {
                var tarEntry = CreateTarEntry(entry, format);
                if (entry is FileTarEntry file)
                {
                    using var contentStream = file.Content.ToStreamSeekable();
                    tarEntry.DataStream = contentStream;
                    writer.WriteEntry(tarEntry);
                }
                else
                {
                    writer.WriteEntry(tarEntry);
                }
            }
        }

        stream.Position = 0;
        return ByteSource.FromBytes(stream.ToArray());
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
}
