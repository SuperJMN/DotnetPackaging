using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using Zafiro.FileSystem.Core;
using Zafiro.FileSystem.Readonly;

namespace DotnetPackaging.Rpm.Builder;

public class RpmBuilder
{
    public FromContainerOptions Container(IContainer root, string? name = null)
    {
        return new FromContainerOptions(root, Maybe<string>.From(name));
    }

    public FromContainerOptions Directory(IDirectory root)
    {
        var containerResult = BuildContainer(root);
        if (containerResult.IsFailure)
        {
            throw new InvalidOperationException($"Unable to convert directory '{root.Name}' into container: {containerResult.Error}");
        }

        return new FromContainerOptions(containerResult.Value, Maybe<string>.From(root.Name));
    }

    private static Result<RootContainer> BuildContainer(IDirectory root)
    {
        return ContainerUtils.BuildContainer(root);
    }
}
