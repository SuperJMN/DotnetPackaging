using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Tests;

public class FromContainerOptions
{
    private readonly ISlimDirectory container;

    public FromContainerOptions(ISlimDirectory container)
    {
        this.container = container;
    }

    public FromContainer Configure(Action<ContainerOptionsSetup> setup)
    {
        var options = new ContainerOptionsSetup();
        setup(options);
        return new FromContainer(container, options);
    }
}