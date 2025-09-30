using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using Zafiro.FileSystem.Core;
using Zafiro.FileSystem.Readonly;

namespace DotnetPackaging.Deb.Builder;

public class DebBuilder
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
        var files = root.RootedFiles()
            .ToDictionary(
                file => file.Path == ZafiroPath.Empty ? file.Name : file.Path.Combine(file.Name).ToString(),
                file => (IByteSource)ByteSource.FromByteObservable(file.Value.Bytes),
                StringComparer.Ordinal);

        return files.ToRootContainer();
    }
}
