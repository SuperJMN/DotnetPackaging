using DotnetPackaging.AppImage.Core;
using Zafiro.FileSystem.Readonly;

namespace DotnetPackaging.AppImage.Builder;

public class AppImageBuilder(RuntimeFactory runtimeFactory)
{
    public FromContainerConfiguration Directory(IDirectory root)
    {
        return new FromContainerConfiguration(runtimeFactory, root);
    }
}