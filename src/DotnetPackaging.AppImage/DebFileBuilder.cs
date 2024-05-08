using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Tests;

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