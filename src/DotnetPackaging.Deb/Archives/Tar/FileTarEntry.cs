using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Tar;

public record FileTarEntry : TarEntry
{
    public IObservableDataStream Content { get; }

    public FileTarEntry(string path, IObservableDataStream content, TarFileProperties properties) : base(path, properties)
    {
        Content = content;
    }
}