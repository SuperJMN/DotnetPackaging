using CSharpFunctionalExtensions;

namespace Zafiro.FileSystem.Unix;

public class UnixFile : UnixNode
{
    public UnixFileProperties Properties { get; }

    public UnixFile(string name, Maybe<UnixFileProperties> properties) : base(name)
    {
        Properties = properties.GetValueOrDefault(UnixFileProperties.RegularFileProperties);
    }

    public UnixFile(string name) : this(name, Maybe<UnixFileProperties>.None) { }
}