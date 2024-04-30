using DotnetPackaging.Deb.Archives.Tar;

namespace DotnetPackaging.Deb.Archives.Deb;

public record DebFile
{
    public PackageMetadata Metadata { get; }
    public TarEntry[] Entries { get; }

    public DebFile(PackageMetadata metadata, params TarEntry[] entries)
    {
        Metadata = metadata;
        Entries = entries;
    }
}