namespace DotnetPackaging.Deb.Archives.Tar;

public record FileTarEntry : TarEntry
{
    public FileTarEntry(string path, byte[] content, TarFileProperties properties) : base(path, properties)
    {
        Content = content;
        Properties = properties;
    }

    public byte[] Content { get; }

    public new TarFileProperties Properties { get; }
}
