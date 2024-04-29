using Zafiro.FileSystem;

namespace DotnetPackaging.Deb.Archives.Tar;

public abstract record TarEntry
{
    protected TarEntry(UnixFileProperties properties)
    {
        Properties = properties;
    }

    public UnixFileProperties Properties { get; }
    public abstract ZafiroPath Path { get; }
}