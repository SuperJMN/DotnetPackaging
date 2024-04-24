using Zafiro.FileSystem;

namespace DotnetPackaging.Deb.Archives.Deb.Contents;

public class RegularContent : Content
{
    public RegularContent(ZafiroPath path, IByteFlow byteFlow) : base(path, byteFlow)
    {
    }
}