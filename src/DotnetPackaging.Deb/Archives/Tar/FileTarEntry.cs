using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Archives.Tar;

public record FileTarEntry : TarEntry
{
    public IByteSource Content { get; }

    public FileTarEntry(string path, IByteSource content, TarFileProperties properties) : base(path, properties)
    {
        Content = content;
    }
}
