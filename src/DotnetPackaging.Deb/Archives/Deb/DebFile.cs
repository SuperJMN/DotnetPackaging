using DotnetPackaging.Deb.Archives.Tar;

namespace DotnetPackaging.Deb.Archives.Deb;

public record DebFile
{
    public Metadata Metadata { get; }
    public TarEntry[] Entries { get; }

    public DebFile(Metadata metadata, params TarEntry[] entries)
    {
        Metadata = metadata;
        Entries = entries;
    }
}