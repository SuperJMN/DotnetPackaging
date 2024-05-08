using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Tests;

public class DebFileBuilder
{
    private readonly FakeRuntimeFactory runtimeFactory;

    public DebFileBuilder(FakeRuntimeFactory runtimeFactory)
    {
        this.runtimeFactory = runtimeFactory;
    }

    public FromContainerOptions FromDirectory(ISlimDirectory root)
    {
        return new FromContainerOptions(runtimeFactory, root);
    }
}