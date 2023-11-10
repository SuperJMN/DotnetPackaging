using DotnetPackaging.Common;
using Zafiro.FileSystem;

namespace DotnetPackaging.New.Archives.Deb.Contents;

public class RegularContent : Content
{
    public RegularContent(ZafiroPath path, IByteFlow byteFlow) : base(path, byteFlow)
    {
    }
}