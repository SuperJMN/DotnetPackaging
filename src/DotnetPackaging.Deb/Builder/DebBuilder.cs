using Zafiro.FileSystem.Readonly;

namespace DotnetPackaging.Deb.Builder;

public class DebBuilder
{
    public FromContainerOptions Directory(IDirectory root)
    {
        return new FromContainerOptions(root);
    }
}