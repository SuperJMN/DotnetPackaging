using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

public class UnixNode : INode
{
    public UnixNode(string name)
    {
        Name = name;
    }

    public string Name { get; }
}