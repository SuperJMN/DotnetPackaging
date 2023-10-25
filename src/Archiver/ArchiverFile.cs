using System.Text;

namespace Archiver;

public class ArchiverFile
{
    public static void Write(Stream fileStream, params FileEntry[] entryValue)
    {
        var streamWriter = new StreamWriter(fileStream, Encoding.ASCII) { NewLine = "\n" };
        using (var writer = streamWriter)
        {
            WriteHeader(writer);
            entryValue.ToList().ForEach(entry => WriteEntry(fileStream, entry, writer));
        }
    }

    private static void WriteHeader(StreamWriter writer)
    {
        writer.WriteLine("!<arch>");
        writer.Write("debian-binary   ");
        writer.Write("1342943816  ");
        writer.Write("0     ");
        writer.Write("0     ");
        writer.Write("100644  ");
        writer.Write("4         ");
        writer.Write("`\n");
        writer.WriteLine("2.0");
    }

    private static void WriteEntry(Stream fileStream, FileEntry entry, StreamWriter writer)
    {
        //
        writer.Write(entry.Name.ToFixed(16));
        writer.Write(entry.DateTimeOffset.ToUnixTimeSeconds().ToString().ToFixed(12));  // Modification timestamp
        writer.Write("0     ");
        writer.Write("0     ");

        writer.Write("100644  ");
        writer.Write(entry.Stream.Length.ToString().ToFixed(10));
        writer.Write("`\n");

        writer.Flush();

        entry.Stream.CopyTo(fileStream);
    }
}