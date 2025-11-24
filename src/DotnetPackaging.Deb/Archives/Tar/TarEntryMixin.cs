using System.Text;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class TarEntryMixin
{
    public static IEnumerable<byte[]> ToChunks(this TarEntry entry)
    {
        return entry switch
        {
            FileTarEntry file => ToFileChunks(file),
            DirectoryTarEntry directory => [CreateHeader(directory.Path, directory.Properties, 0, '5')],
            _ => throw new NotSupportedException($"Unsupported TAR entry type: {entry.GetType().Name}")
        };
    }

    private static IEnumerable<byte[]> ToFileChunks(FileTarEntry entry)
    {
        var header = CreateHeader(entry.Path, entry.Properties, entry.Content.LongLength, '0');
        yield return header;
        yield return entry.Content;

        var padding = Padding(entry.Content.Length);
        if (padding.Length > 0)
        {
            yield return padding;
        }
    }

    private static byte[] CreateHeader(string path, TarEntryProperties properties, long fileLength, char typeFlag)
    {
        var header = new byte[512];
        WriteField(header, 0, 100, Truncate(path, 100));
        WriteField(header, 100, 8, FormatMode(properties.Permissions));
        WriteField(header, 108, 8, FormatNumber(properties.OwnerId.Match(id => (long)id, () => 0), 7));
        WriteField(header, 116, 8, FormatNumber(properties.GroupId.Match(id => (long)id, () => 0), 7));
        WriteField(header, 124, 12, FormatNumber(fileLength, 11));
        WriteField(header, 136, 12, FormatNumber(CoerceLastModification(properties.LastModification), 11));
        WriteField(header, 148, 8, new string(' ', 8));
        WriteField(header, 156, 1, typeFlag.ToString());
        WriteField(header, 157, 100, new string('\0', 100));
        WriteField(header, 257, 6, "ustar ");
        WriteField(header, 263, 2, " \0");
        WriteField(header, 265, 32, FormatText(properties.OwnerUsername));
        WriteField(header, 297, 32, FormatText(properties.GroupName));

        var checksum = CalculateChecksum(header);
        WriteField(header, 148, 8, FormatChecksum(checksum));

        return header;
    }

    private static string FormatChecksum(long checksum) => $"{Convert.ToString(checksum, 8).PadLeft(6, '0')}\0 ";

    private static string FormatText(Maybe<string> text) => text.Match(s => s.PadRight(32, '\0'), () => new string('\0', 32));

    private static string FormatMode(UnixPermissions permissions)
    {
        var owner = PermissionDigit(permissions.OwnerRead, permissions.OwnerWrite, permissions.OwnerExec);
        var group = PermissionDigit(permissions.GroupRead, permissions.GroupWrite, permissions.GroupExec);
        var other = PermissionDigit(permissions.OtherRead, permissions.OtherWrite, permissions.OtherExec);
        var mode = $"{owner}{group}{other}";
        return $"{mode}\0".PadLeft(8, '0');
    }

    private static string FormatNumber(long number, int digits)
    {
        var octal = Convert.ToString(number, 8);
        return octal.PadLeft(digits, '0') + "\0";
    }

    private static int PermissionDigit(bool read, bool write, bool execute) => (read ? 4 : 0) + (write ? 2 : 0) + (execute ? 1 : 0);

    private static long CoerceLastModification(DateTimeOffset modification) => Math.Max(0, modification.ToUnixTimeSeconds());

    private static void WriteField(byte[] header, int offset, int length, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, 0, header, offset, Math.Min(length, bytes.Length));
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value.PadRight(maxLength, '\0');
        }

        return value[..maxLength];
    }

    private static byte[] Padding(long contentLength)
    {
        var paddingLength = (int)(512 - (contentLength % 512)) % 512;
        return paddingLength == 0 ? Array.Empty<byte>() : new byte[paddingLength];
    }

    private static long CalculateChecksum(byte[] header)
    {
        long checksum = 0;
        foreach (var b in header)
        {
            checksum += b;
        }

        return checksum;
    }
}
