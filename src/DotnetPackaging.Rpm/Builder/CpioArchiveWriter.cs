using System.Text;

namespace DotnetPackaging.Rpm.Builder;

internal static class CpioArchiveWriter
{
    private const string Magic = "070701";

    public static byte[] Build(IReadOnlyList<RpmFileEntry> entries)
    {
        using var stream = new MemoryStream();
        foreach (var entry in entries)
        {
            WriteEntry(stream, entry);
        }

        WriteTrailer(stream);
        return stream.ToArray();
    }

    private static void WriteEntry(Stream stream, RpmFileEntry entry)
    {
        var name = entry.Path.TrimStart('/');
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var nameSize = nameBytes.Length + 1;
        var fileSize = entry.IsDirectory ? 0 : entry.Data.Length;
        var nlink = entry.IsDirectory ? 2 : 1;

        WriteHeader(stream, entry, nameSize, fileSize, nlink);
        stream.Write(nameBytes, 0, nameBytes.Length);
        stream.WriteByte(0);
        PadToFour(stream, nameSize);

        if (!entry.IsDirectory)
        {
            stream.Write(entry.Data, 0, entry.Data.Length);
            PadToFour(stream, entry.Data.Length);
        }
    }

    private static void WriteTrailer(Stream stream)
    {
        var name = "TRAILER!!!";
        var nameBytes = Encoding.ASCII.GetBytes(name);
        var nameSize = nameBytes.Length + 1;
        var entry = new RpmFileEntry(
            name,
            name,
            0,
            0,
            0,
            0,
            string.Empty,
            "root",
            "root",
            true,
            0,
            Array.Empty<byte>());

        WriteHeader(stream, entry, nameSize, 0, 1);
        stream.Write(nameBytes, 0, nameBytes.Length);
        stream.WriteByte(0);
        PadToFour(stream, nameSize);
    }

    private static void WriteHeader(Stream stream, RpmFileEntry entry, int nameSize, int fileSize, int nlink)
    {
        WriteAscii(stream, Magic);
        WriteHex(stream, entry.Inode);
        WriteHex(stream, entry.Mode);
        WriteHex(stream, 0);
        WriteHex(stream, 0);
        WriteHex(stream, nlink);
        WriteHex(stream, entry.MTime);
        WriteHex(stream, fileSize);
        WriteHex(stream, 0);
        WriteHex(stream, 0);
        WriteHex(stream, 0);
        WriteHex(stream, 0);
        WriteHex(stream, nameSize);
        WriteHex(stream, 0);
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteHex(Stream stream, int value)
    {
        var hex = value.ToString("x8");
        var bytes = Encoding.ASCII.GetBytes(hex);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void PadToFour(Stream stream, int size)
    {
        var padding = size % 4 == 0 ? 0 : 4 - (size % 4);
        for (var i = 0; i < padding; i++)
        {
            stream.WriteByte(0);
        }
    }
}
