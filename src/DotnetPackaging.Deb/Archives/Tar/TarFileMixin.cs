using System.Linq;
using System.Reactive.Linq;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class TarFileMixin
{
    public static IByteSource ToByteSource(this TarFile tarFile)
    {
        var chunks = tarFile.Entries.SelectMany(entry => entry.ToChunks())
            .Concat(new[] { new byte[1024] });
        return ByteSource.FromByteChunks(chunks.ToObservable());
    }

    public static long Size(this TarFile tarFile)
    {
        var entriesLength = tarFile.Entries.Sum(EntryLength);
        return entriesLength + 1024;
    }

    private static long EntryLength(TarEntry entry) => entry switch
    {
        FileTarEntry file => 512 + file.Content.LongLength + PaddingLength(file.Content.Length),
        DirectoryTarEntry => 512,
        _ => throw new NotSupportedException($"Unsupported TAR entry type: {entry.GetType().Name}")
    };

    private static int PaddingLength(long contentLength)
    {
        return (int)(512 - (contentLength % 512)) % 512;
    }
}
