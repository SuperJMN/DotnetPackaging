using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Tar;

public abstract record TarEntry
{
    protected TarEntry(UnixFileProperties properties)
    {
        Properties = properties;
    }

    public UnixFileProperties Properties { get; }
}

public record DirectoryTarEntry : TarEntry
{
    public DirectoryTarEntry(ZafiroPath path, TarDirectoryProperties properties) :base(properties)
    {
        Path = path;
    }

    public ZafiroPath Path { get; }
}

public record TarDirectoryProperties : UnixFileProperties
{
}

public record FileTarEntry : TarEntry
{
    public RootedFile File { get; }

    public FileTarEntry(RootedFile file, TarFileProperties properties) : base(properties)
    {
        File = file;
    }
}

public record TarFileProperties : UnixFileProperties
{
}