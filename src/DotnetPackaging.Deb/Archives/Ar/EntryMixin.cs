using System.Text;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class EntryMixin 
{
    public static IByteProvider ToByteProvider(this Entry entry)
    {
        return new ComposedByteProvider
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
    private static IByteProvider FileIdentifier(this Entry entry) => new StringByteProvider(entry.File.Name.PadRight(16), Encoding.ASCII);
    // 16  12  File modification timestamp (in seconds) Decimal
    private static IByteProvider FileModificationTimestamp(this Entry entry) => new StringByteProvider(entry.Properties.LastModification.ToUnixTimeSeconds().ToString().PadRight(12), Encoding.ASCII);
    // 28  6   Owner ID Decimal
    private static IByteProvider OwnerId(this Entry entry) => new StringByteProvider(entry.Properties.OwnerId.GetValueOrDefault().ToString().PadRight(6), Encoding.ASCII);
    // 34  6   Group ID Decimal
    private static IByteProvider GroupId(this Entry entry) => new StringByteProvider(entry.Properties.GroupId.GetValueOrDefault().ToString().PadRight(6), Encoding.ASCII);
    // 40  8   File mode (type and permission) Octal
    private static IByteProvider FileMode(this Entry entry) => new StringByteProvider(("100" + entry.Properties.FileMode.ToFileMode()).PadRight(8), Encoding.ASCII);
    // 48  10  File size in bytes Decimal
    private static IByteProvider FileSize(this Entry entry) => new StringByteProvider(entry.File.Length.ToString().PadRight(10), Encoding.ASCII);
    // 58  2   Ending characters 0x60 0x0A
    private static IByteProvider Ending(this Entry entry) => new StringByteProvider("`\n", Encoding.ASCII);
}