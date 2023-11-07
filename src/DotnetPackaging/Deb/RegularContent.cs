using DotnetPackaging.Common;
using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

public class RegularContent : Content
{
    public RegularContent(ZafiroPath path, IByteStore byteStore) : base(path, byteStore)
    {
    }
}