using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Tar;

public record FileTarEntry : TarEntry
{
    public RootedFile File { get; }

    public FileTarEntry(RootedFile file, TarFileProperties properties) : base(properties)
    {
        File = file;
    }

    public override ZafiroPath Path => File.FullPath();
}