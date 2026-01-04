using System.Security.Cryptography;
using System.Text;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Rpm.Builder;

internal static class RpmFileListBuilder
{
    public static RpmFileList Build(RpmLayout layout, DateTimeOffset modificationTime)
    {
        var entries = new List<RpmFileEntry>();
        var dirNames = new List<string>();
        var dirIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var mtime = (int)modificationTime.ToUnixTimeSeconds();
        var inode = 1;

        foreach (var entry in layout.Entries)
        {
            var path = NormalizePath(entry.Path);
            var (dirName, baseName) = SplitPath(path);
            var dirKey = NormalizeDirName(dirName);
            var index = GetDirIndex(dirKey, dirNames, dirIndex);
            var mode = entry.Type == RpmEntryType.Directory
                ? 0x4000 | entry.Properties.FileMode
                : 0x8000 | entry.Properties.FileMode;

            var data = entry.Type == RpmEntryType.File
                ? entry.Content?.Array() ?? throw new InvalidOperationException($"Entry '{entry.Path}' is missing content")
                : Array.Empty<byte>();

            var digest = entry.Type == RpmEntryType.File ? ComputeMd5Hex(data) : string.Empty;
            var size = entry.Type == RpmEntryType.File ? data.Length : 0;

            entries.Add(new RpmFileEntry(
                path,
                baseName,
                index,
                mode,
                size,
                mtime,
                digest,
                entry.Properties.OwnerUsername,
                entry.Properties.GroupName,
                entry.Type == RpmEntryType.Directory,
                inode++,
                data));
        }

        return new RpmFileList(entries, dirNames);
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace("\\", "/", StringComparison.Ordinal);
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = $"/{normalized.TrimStart('.')}";
        }

        return normalized;
    }

    private static (string DirName, string BaseName) SplitPath(string path)
    {
        var trimmed = path.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        if (lastSlash < 0)
        {
            return ("/", trimmed);
        }

        var dir = lastSlash == 0 ? "/" : trimmed[..(lastSlash + 1)];
        var baseName = trimmed[(lastSlash + 1)..];
        return (dir, baseName);
    }

    private static string NormalizeDirName(string dirName)
    {
        if (string.Equals(dirName, "/", StringComparison.Ordinal))
        {
            return "/";
        }

        return dirName.EndsWith("/", StringComparison.Ordinal) ? dirName : $"{dirName}/";
    }

    private static int GetDirIndex(string dirName, ICollection<string> dirNames, IDictionary<string, int> dirIndex)
    {
        if (dirIndex.TryGetValue(dirName, out var index))
        {
            return index;
        }

        index = dirNames.Count;
        dirNames.Add(dirName);
        dirIndex[dirName] = index;
        return index;
    }

    private static string ComputeMd5Hex(byte[] data)
    {
        var hash = MD5.HashData(data);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }
}

internal sealed record RpmFileList(IReadOnlyList<RpmFileEntry> Entries, IReadOnlyList<string> DirNames);

internal sealed record RpmFileEntry(
    string Path,
    string BaseName,
    int DirIndex,
    int Mode,
    int Size,
    int MTime,
    string Digest,
    string UserName,
    string GroupName,
    bool IsDirectory,
    int Inode,
    byte[] Data);
