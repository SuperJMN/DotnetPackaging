namespace DotnetPackaging.Deb.Tests;

internal static class TarArchiveReader
{
    public static IReadOnlyList<TarEntryInfo> ReadEntries(byte[] tarBytes)
    {
        using var stream = new MemoryStream(tarBytes);
        using var reader = new TarReader(stream);
        var entries = new List<TarEntryInfo>();

        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            var data = ReadEntryData(entry);
            entries.Add(new TarEntryInfo(Normalize(entry.Name), entry.EntryType, entry.Mode, data));
        }

        return entries;
    }

    private static byte[] ReadEntryData(TarEntry entry)
    {
        if (entry.DataStream is null)
        {
            return Array.Empty<byte>();
        }

        using var buffer = new MemoryStream();
        entry.DataStream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string Normalize(string name)
    {
        var normalized = name.Replace("\\", "/", StringComparison.Ordinal);
        normalized = normalized.TrimStart('.');
        normalized = normalized.TrimStart('/');
        return normalized;
    }
}

internal sealed record TarEntryInfo(string Name, TarEntryType EntryType, UnixFileMode Mode, byte[] Data)
{
    public bool IsDirectory => EntryType == TarEntryType.Directory;
}
