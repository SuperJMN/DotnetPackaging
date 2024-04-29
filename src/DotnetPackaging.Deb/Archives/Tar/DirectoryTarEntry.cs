using Zafiro.FileSystem;

namespace DotnetPackaging.Deb.Archives.Tar;

public record DirectoryTarEntry : TarEntry
{
    public DirectoryTarEntry(ZafiroPath path, TarDirectoryProperties properties) : base(properties)
    {
        Path = path;
    }

    public override ZafiroPath Path { get; }
}