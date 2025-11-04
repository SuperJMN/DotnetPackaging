using DotnetPackaging;
using Zafiro.DataModel;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.Rpm;

public record RpmPackage(PackageMetadata Metadata, IReadOnlyList<RpmEntry> Entries);

public record RpmEntry(string Path, UnixFileProperties Properties, IData? Content, RpmEntryType Type);

public enum RpmEntryType
{
    File,
    Directory
}
