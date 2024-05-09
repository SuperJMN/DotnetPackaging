using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.Deb.Archives.Tar;

public abstract record TarEntry
{
    protected TarEntry(string path, UnixFileProperties properties)
    {
        Path = path;
        Properties = properties;
    }

    public UnixFileProperties Properties { get; }
    public string Path { get; }
}