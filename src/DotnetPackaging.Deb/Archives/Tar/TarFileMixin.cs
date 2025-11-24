using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class TarFileMixin
{
    public static IByteSource ToByteSource(this TarFile tarFile)
    {
        var chunks = tarFile.Entries.SelectMany(entry => entry.ToChunks()).ToList();
        chunks.Add(new byte[1024]);
        return ByteSource.FromByteChunks(chunks);
    }
}
