using System.Text;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class EntryMixin 
{
    public static IData ToByteProvider(this Entry entry)
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
            entry.File
        );
    }
    // 0   16  File identifier ASCII
    private static IData FileIdentifier(this Entry entry) => new StringData(entry.File.Name.PadRight(16), Encoding.ASCII);
    // 16  12  File modification timestamp (in seconds) Decimal
    private static IData FileModificationTimestamp(this Entry entry) => new StringData(entry.Properties.LastModification.ToUnixTimeSeconds().ToString().PadRight(12), Encoding.ASCII);
    // 28  6   Owner ID Decimal
    private static IData OwnerId(this Entry entry) => new StringData(entry.Properties.OwnerId.GetValueOrDefault().ToString().PadRight(6), Encoding.ASCII);
    // 34  6   Group ID Decimal
    private static IData GroupId(this Entry entry) => new StringData(entry.Properties.GroupId.GetValueOrDefault().ToString().PadRight(6), Encoding.ASCII);
    // 40  8   File mode (type and permission) Octal
    private static IData FileMode(this Entry entry) => new StringData(("100" + entry.Properties.FileMode.ToFileModeString()).PadRight(8), Encoding.ASCII);
    // 48  10  File size in bytes Decimal
    private static IData FileSize(this Entry entry) => new StringData(entry.File.Length.ToString().PadRight(10), Encoding.ASCII);
    // 58  2   Ending characters 0x60 0x0A
    private static IData Ending(this Entry entry) => new StringData("`\n", Encoding.ASCII);
}