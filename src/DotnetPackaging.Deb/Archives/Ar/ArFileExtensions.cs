using System.Text;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class ArFileExtensions
{
    private const string Signature = "!<arch>\n";

    public static IByteSource ToByteSource(this ArFile arFile)
    {
        using var stream = new MemoryStream();
        WriteAscii(stream, Signature);

        foreach (var entry in arFile.Entries)
        {
            var contentBytes = entry.Content.Array();
            var header = BuildHeader(entry, contentBytes.LongLength);
            WriteAscii(stream, header);
            stream.Write(contentBytes, 0, contentBytes.Length);

            if (contentBytes.LongLength % 2 != 0)
            {
                stream.WriteByte((byte)'\n');
            }
        }

        stream.Position = 0;
        return ByteSource.FromBytes(stream.ToArray());
    }

    private static string BuildHeader(ArEntry entry, long size)
    {
        var modeOctal = Convert.ToString(entry.Properties.Mode, 8);

        return
            $"{Field(entry.Name, 16)}" +
            $"{Field(entry.Properties.LastModification.ToUnixTimeSeconds().ToString(), 12)}" +
            $"{Field(entry.Properties.OwnerId.ToString(), 6)}" +
            $"{Field(entry.Properties.GroupId.ToString(), 6)}" +
            $"{Field($"100{modeOctal}", 8)}" +
            $"{Field(size.ToString(), 10)}" +
            "`\n";
    }

    private static string Field(string value, int width)
    {
        if (value.Length > width)
        {
            return value[..width];
        }

        return value.PadRight(width);
    }

    private static void WriteAscii(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }
}
