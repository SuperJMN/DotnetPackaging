using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Builder;

internal class FromContainerOptions
{
    private readonly IContainer container;
    private readonly Maybe<string> containerName;

    public FromContainerOptions(IContainer container, Maybe<string> containerName)
    {
        this.container = container ?? throw new ArgumentNullException(nameof(container));
        this.containerName = containerName;
    }

    public FromContainer Configure(Action<FromDirectoryOptions> setup)
    {
        var options = new FromDirectoryOptions();
        setup(options);
        return new FromContainer(container, options, containerName);
    }
}
