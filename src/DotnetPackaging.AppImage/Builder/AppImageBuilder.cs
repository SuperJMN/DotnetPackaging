namespace DotnetPackaging.AppImage.Builder;

public class AppImageBuilder
{
    private readonly RuntimeFactory runtimeFactory;

    public AppImageBuilder(RuntimeFactory runtimeFactory)
    {
        this.runtimeFactory = runtimeFactory;
    }

    public FromContainerOptions FromDirectory(ISlimDirectory root)
    {
        return new FromContainerOptions(runtimeFactory, root);
    }
}