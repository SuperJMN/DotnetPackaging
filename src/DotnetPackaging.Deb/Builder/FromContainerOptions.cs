namespace DotnetPackaging.Deb.Builder;

public class FromContainerOptions
{
    private readonly IDirectory container;

    public FromContainerOptions(IDirectory container)
    {
        this.container = container;
    }

    public FromContainer Configure(Action<FromDirectoryOptions> setup)
    {
        var options = new FromDirectoryOptions();
        setup(options);
        return new FromContainer(container, options);
    }
}