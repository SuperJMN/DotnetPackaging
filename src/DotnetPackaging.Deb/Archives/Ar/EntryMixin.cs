using System.Text;
using DotnetPackaging.Deb.Bytes;
using DotnetPackaging.Deb.Unix;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class EntryMixin
{
    public static IData ToData(this Entry entry)
    {
        return new CompositeData
        (
            entry.FileIdentifier(),
            entry.FileModificationTimestamp(),
            entry.OwnerId(),
            entry.GroupId(),
            entry.FileMode(),
            entry.FileSize(),
            entry.Ending(),
            entry.Content
        );
    }
    // 0   16  File identifier ASCII
    private static IData FileIdentifier(this Entry entry) => Data.FromString(entry.Name.PadRight(16), Encoding.ASCII);
    // 16  12  File modification timestamp (in seconds) Decimal
    private static IData FileModificationTimestamp(this Entry entry) => Data.FromString(entry.Properties.LastModification.ToUnixTimeSeconds().ToString().PadRight(12), Encoding.ASCII);
    // 28  6   Owner ID Decimal
    private static IData OwnerId(this Entry entry) => Data.FromString(entry.Properties.OwnerId.GetValueOrDefault().ToString().PadRight(6), Encoding.ASCII);
    // 34  6   Group ID Decimal
    private static IData GroupId(this Entry entry) => Data.FromString(entry.Properties.GroupId.GetValueOrDefault().ToString().PadRight(6), Encoding.ASCII);
    // 40  8   File mode (type and permission) Octal
    private static IData FileMode(this Entry entry)
    {
        var permissionBits = entry.Properties.FileMode.ToFileModeString().TrimStart('0');
        var mode = "100" + (permissionBits.Length == 0 ? "0" : permissionBits);

        return Data.FromString(mode.PadRight(8), Encoding.ASCII);
    }
    // 48  10  File size in bytes Decimal
    private static IData FileSize(this Entry entry) => Data.FromString(entry.Content.Length.ToString().PadRight(10), Encoding.ASCII);
    // 58  2   Ending characters 0x60 0x0A
    private static IData Ending(this Entry entry) => Data.FromString("`\n", Encoding.ASCII);
}
