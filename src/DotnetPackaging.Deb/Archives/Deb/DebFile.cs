using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Tar;

namespace DotnetPackaging.Deb.Archives.Deb;

internal record DebFile
{
    public PackageMetadata Metadata { get; }
    public TarEntry[] Entries { get; }
    public MaintainerScripts Scripts { get; }

    public DebFile(PackageMetadata metadata, TarEntry[] entries, MaintainerScripts? scripts = null)
    {
        Metadata = metadata;
        Entries = entries;
        Scripts = scripts ?? MaintainerScripts.None;
    }
}

internal record MaintainerScripts(Maybe<string> PostInst, Maybe<string> PreRm, Maybe<string> PostRm)
{
    public static MaintainerScripts None => new(Maybe<string>.None, Maybe<string>.None, Maybe<string>.None);
}
