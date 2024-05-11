using DotnetPackaging.AppImage.Builder;

namespace DotnetPackaging.Deb.Builder;

public class FromContainerOptions
{
    private readonly IDirectory container;

    public FromContainerOptions(IDirectory container)
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