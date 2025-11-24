using System.Reactive.Linq;
using System.Text;
using Zafiro.DivineBytes;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class EntryMixin
{
    public static IObservable<byte[]> ToObservable(this ArEntry entry)
    {
        var padding = entry.Length % 2 != 0 ? Observable.Return(new[] { (byte)'\n' }) : Observable.Empty<byte[]>();

        return Observable.Return(entry.ToHeader())
            .Concat(entry.Content.Bytes)
            .Concat(padding);
    }

    private static byte[] ToHeader(this ArEntry entry)
    {
        var header = new byte[60];
        WriteField(header, 0, 16, TrimName(entry.Name));
        WriteField(header, 16, 12, Seconds(entry.Properties.LastModification).ToString().PadRight(12));
        WriteField(header, 28, 6, entry.Properties.OwnerId.Match(id => id, () => 0).ToString().PadRight(6));
        WriteField(header, 34, 6, entry.Properties.GroupId.Match(id => id, () => 0).ToString().PadRight(6));
        WriteField(header, 40, 8, FormatFileMode(entry.Properties.Permissions).PadRight(8));
        WriteField(header, 48, 10, entry.Length.ToString().PadRight(10));
        WriteField(header, 58, 2, "`\n");
        return header;
    }

    private static void WriteField(byte[] header, int offset, int length, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, 0, header, offset, Math.Min(length, bytes.Length));
    }

    private static string TrimName(string name)
    {
        return name.Length <= 16 ? name.PadRight(16) : name[..16];
    }

    private static long Seconds(DateTimeOffset dateTime) => dateTime.ToUnixTimeSeconds();

    private static string FormatFileMode(UnixPermissions permissions) => $"100{ToOctal(permissions)}";

    private static string ToOctal(UnixPermissions permissions)
    {
        var owner = PermissionDigit(permissions.OwnerRead, permissions.OwnerWrite, permissions.OwnerExec);
        var group = PermissionDigit(permissions.GroupRead, permissions.GroupWrite, permissions.GroupExec);
        var other = PermissionDigit(permissions.OtherRead, permissions.OtherWrite, permissions.OtherExec);
        return $"{owner}{group}{other}";
    }

    private static int PermissionDigit(bool read, bool write, bool execute) => (read ? 4 : 0) + (write ? 2 : 0) + (execute ? 1 : 0);
}
