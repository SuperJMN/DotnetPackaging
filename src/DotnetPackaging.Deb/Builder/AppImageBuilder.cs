using DotnetPackaging.Deb.Builder;

namespace DotnetPackaging.AppImage.Builder;

public class DebBuilder
{

    public DebBuilder()
    {
    }

    public FromContainerOptions FromDirectory(IDirectory root)
    {
        return new FromContainerOptions(root);
    }
}