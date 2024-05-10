using DotnetPackaging.AppImage.Tests;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage;

public class DebFileBuilder
{
    private readonly RuntimeFactory runtimeFactory;

    public DebFileBuilder(RuntimeFactory runtimeFactory)
    {
        this.runtimeFactory = runtimeFactory;
    }

    public FromContainerOptions FromDirectory(ISlimDirectory root)
    {
        return new FromContainerOptions(runtimeFactory, root);
    }
}