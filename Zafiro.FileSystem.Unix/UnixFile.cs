using CSharpFunctionalExtensions;
using DotnetPackaging;

namespace Zafiro.FileSystem.Unix;

public class UnixFile : UnixNode, IData
{
    public UnixFile(string name, IData data) : this(name, data, Maybe<UnixFileProperties>.None)
    {
    }

    public UnixFile(string name, IData data, Maybe<UnixFileProperties> properties) : base(name)
    {
        Data = data;
        Properties = properties.GetValueOrDefault(UnixFileProperties.RegularFileProperties);
    }

    public UnixFile(string name) : this(name, new ByteArrayData(Array.Empty<byte>()), Maybe<UnixFileProperties>.None)
    {
    }

    public IData Data { get; }
    public UnixFileProperties Properties { get; }
    public IObservable<byte[]> Bytes => Data.Bytes;
    public long Length => Data.Length;
}