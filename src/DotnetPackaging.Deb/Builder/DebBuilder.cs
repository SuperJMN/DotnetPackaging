namespace DotnetPackaging.Deb.Builder;

public class DebBuilder
{
    public FromContainerOptions FromDirectory(IDirectory root)
    {
        return new FromContainerOptions(root);
    }
}