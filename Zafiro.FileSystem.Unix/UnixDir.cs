namespace Zafiro.FileSystem.Unix;

public class UnixDir : UnixNode
{
    public UnixFileProperties Properties { get; }

    public UnixDir(string name, IEnumerable<UnixNode> nodes) : this(name, Maybe.From(nodes), Maybe<UnixFileProperties>.None){ }
    
    public UnixDir(string name) : this(name, Maybe<IEnumerable<UnixNode>>.None, Maybe<UnixFileProperties>.None){ }
    
    public UnixDir(string name, Maybe<UnixFileProperties> properties) : this(name, Maybe<IEnumerable<UnixNode>>.None, properties){ }
    
    public UnixDir(Maybe<string> name, Maybe<IEnumerable<UnixNode>> nodes, Maybe<UnixFileProperties> properties) : base(name.GetValueOrDefault(""))
    {
        Nodes = nodes.GetValueOrDefault(Enumerable.Empty<UnixNode>());
        Properties = properties.GetValueOrDefault(UnixFileProperties.RegularDirectoryProperties);
    }

    public IEnumerable<UnixNode> Nodes { get; }
}