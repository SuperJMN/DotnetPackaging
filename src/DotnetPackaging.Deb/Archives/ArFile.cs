using CSharpFunctionalExtensions;
using System.Xml.Linq;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives;

public record ArFile(params Entry[] Entries);

public record Entry(IFile File, Properties Properties)
{
}

public record Properties
{
    public required DateTimeOffset LastModification { get; init; }
    public required UnixFilePermissions FileMode { get; init; }
    public required Maybe<int> OwnerId { get; init; }
    public required Maybe<int> GroupId { get; init; }
}