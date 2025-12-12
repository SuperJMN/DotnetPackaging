using System.Reactive.Linq;
using System.Text;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class ArFileExtensions
{
    private const string Signature = "!<arch>\n";

    public static IByteSource ToByteSource(this ArFile arFile)
    {
        var parts = new List<IObservable<byte[]>> { ByteSource.FromString(Signature, Encoding.ASCII).Bytes };

        foreach (var entry in arFile.Entries)
        {
            var size = entry.Content.GetSize().GetAwaiter().GetResult();
            var header = BuildHeader(entry, size);

            parts.Add(ByteSource.FromString(header, Encoding.ASCII).Bytes);
            parts.Add(entry.Content.Bytes);

            if (size % 2 != 0)
            {
                parts.Add(ByteSource.FromBytes(new[] { (byte)'\n' }).Bytes);
            }
        }

        return ByteSource.FromByteObservable(parts.Aggregate(Observable.Concat));
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
}
