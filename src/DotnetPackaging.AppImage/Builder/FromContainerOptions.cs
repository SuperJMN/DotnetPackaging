using DotnetPackaging.AppImage.Kernel;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Builder;

public class FromContainerOptions
{
    private readonly RuntimeFactory runtimeFactory;
    private readonly ISlimDirectory container;

    public FromContainerOptions(RuntimeFactory runtimeFactory, ISlimDirectory container)
    {
        this.runtimeFactory = runtimeFactory;
        this.container = container;
    }

    public FromContainer Configure(Action<ContainerOptionsSetup> setup)
    {
        var options = new ContainerOptionsSetup();
        setup(options);
        return new FromContainer(container, runtimeFactory, options);
    }
}