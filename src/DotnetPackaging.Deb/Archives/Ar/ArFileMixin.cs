using System.Text;
using System.Reactive.Linq;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class ArFileMixin
{
    public static IByteSource ToByteSource(this ArFile arFile)
    {
        var chunks = new List<byte[]> { Encoding.ASCII.GetBytes("!<arch>\n") };
        foreach (var entry in arFile.Entries)
        {
            chunks.AddRange(entry.ToChunks());
        }

        return ByteSource.FromByteChunks(chunks.ToObservable());
    }
}
