using Zafiro.DataModel;

namespace DotnetPackaging.Deb.Archives.Tar;

public record FileTarEntry : TarEntry
{
    public IData Content { get; }

    public FileTarEntry(string path, IData content, TarFileProperties properties) : base(path, properties)
    {
        Content = content;
    }
}