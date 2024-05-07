using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Tests;

public class DebFileBuilder
{
    public FromContainerOptions FromDirectory(ISlimDirectory root)
    {
        return new FromContainerOptions(root);
    }
}