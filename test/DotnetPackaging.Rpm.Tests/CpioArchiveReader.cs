namespace DotnetPackaging.Rpm.Tests;

internal static class CpioArchiveReader
{
    private const string Magic = "070701";
    private const string MagicCrc = "070702";

    public static IReadOnlyList<CpioEntry> Read(byte[] payload)
    {
        var entries = new List<CpioEntry>();
        var position = 0;

        while (position + 110 <= payload.Length)
        {
            var magic = Encoding.ASCII.GetString(payload, position, 6);
            if (magic != Magic && magic != MagicCrc)
            {
                throw new InvalidDataException($"Unexpected cpio magic '{magic}'.");
            }

            var inode = ReadHex(payload, position + 6);
            var mode = ReadHex(payload, position + 14);
            var uid = ReadHex(payload, position + 22);
            var gid = ReadHex(payload, position + 30);
            var nlink = ReadHex(payload, position + 38);
            var mtime = ReadHex(payload, position + 46);
            var fileSize = ReadHex(payload, position + 54);
            var devMajor = ReadHex(payload, position + 62);
            var devMinor = ReadHex(payload, position + 70);
            var rdevMajor = ReadHex(payload, position + 78);
            var rdevMinor = ReadHex(payload, position + 86);
            var nameSize = ReadHex(payload, position + 94);
            var check = ReadHex(payload, position + 102);

            position += 110;
            var name = Encoding.UTF8.GetString(payload, position, nameSize - 1);
            position += nameSize;
            position = Align(position, 4);

            if (name == "TRAILER!!!")
            {
                break;
            }

            var data = payload.AsSpan(position, fileSize).ToArray();
            position += fileSize;
            position = Align(position, 4);

            entries.Add(new CpioEntry(name, data, mode, uid, gid, mtime, inode, nlink, devMajor, devMinor, rdevMajor, rdevMinor, check));
        }

        return entries;
    }

    private static int ReadHex(byte[] buffer, int offset)
    {
        var hex = Encoding.ASCII.GetString(buffer, offset, 8);
        return int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static int Align(int value, int alignment)
    {
        var remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }
}

internal sealed record CpioEntry(
    string Name,
    byte[] Data,
    int Mode,
    int UserId,
    int GroupId,
    int MTime,
    int Inode,
    int LinkCount,
    int DevMajor,
    int DevMinor,
    int RdevMajor,
    int RdevMinor,
    int Checksum);
