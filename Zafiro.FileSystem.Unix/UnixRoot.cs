namespace Zafiro.FileSystem.Unix;

public class UnixRoot : UnixDir
{
    public UnixRoot() : base("")
    {
    }
    
    public UnixRoot(IEnumerable<UnixNode> nodes) : base("", nodes)
    {
    }
}