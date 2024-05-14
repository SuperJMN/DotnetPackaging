using DotnetPackaging.AppImage.Core;

namespace DotnetPackaging.AppImage.Builder;

public class FromContainerConfiguration
{
    private readonly RuntimeFactory runtimeFactory;
    private readonly IDirectory container;

    public FromContainerConfiguration(RuntimeFactory runtimeFactory, IDirectory container)
    {
        this.runtimeFactory = runtimeFactory;
        this.container = container;
    }

    public FromContainer Configure(Action<FromDirectoryOptions> setup)
    {
        var options = new FromDirectoryOptions();
        setup(options);
        return new FromContainer(container, runtimeFactory, options);
    }
}