using DotnetPackaging.Common;
using Zafiro.FileSystem;

namespace DotnetPackaging.New.Deb.Contents;

public class RegularContent : Content
{
    public RegularContent(ZafiroPath path, IByteFlow byteFlow) : base(path, byteFlow)
    {
    }
}