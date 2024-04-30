namespace DotnetPackaging.Deb.Archives.Tar;

public record DirectoryTarEntry : TarEntry
{
    public DirectoryTarEntry(string path, TarDirectoryProperties properties) : base(path, properties)
    {
    }
}