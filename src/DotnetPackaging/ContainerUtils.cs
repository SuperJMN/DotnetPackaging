using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using Zafiro.FileSystem.Core;
using Zafiro.FileSystem.Readonly;

namespace DotnetPackaging;

public static class ContainerUtils
{
    public static Result<RootContainer> BuildContainer(IDirectory root)
    {
        var files = root.RootedFiles()
            .ToDictionary(
                file => file.Path == ZafiroPath.Empty ? file.Name : file.Path.Combine(file.Name).ToString(),
                file => (IByteSource)ByteSource.FromByteObservable(file.Value.Bytes),
                StringComparer.Ordinal);

        return files.ToRootContainer();
    }
}