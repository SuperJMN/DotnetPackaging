using DotnetPackaging.AppImage.Core;

namespace DotnetPackaging.AppImage.Builder;

public class AppImageBuilder
{
    private readonly RuntimeFactory runtimeFactory;

    public AppImageBuilder(RuntimeFactory runtimeFactory)
    {
        this.runtimeFactory = runtimeFactory;
    }

    public FromContainerConfiguration Directory(IDirectory root)
    {
        return new FromContainerConfiguration(runtimeFactory, root);
    }
}