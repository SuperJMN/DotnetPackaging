using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Tar;

public record FileTarEntry : TarEntry
{
    public IByteProvider Content { get; }

    public FileTarEntry(string path, IByteProvider content, TarFileProperties properties) : base(path, properties)
    {
        Content = content;
    }
}