namespace DotnetPackaging.AppImage.Builder;

public class FromContainerOptions
{
    private readonly RuntimeFactory runtimeFactory;
    private readonly IDirectory container;

    public FromContainerOptions(RuntimeFactory runtimeFactory, IDirectory container)
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