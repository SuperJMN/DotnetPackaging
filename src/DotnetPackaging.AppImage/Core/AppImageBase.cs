using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public abstract class AppImageBase
{
    public IRuntime Runtime { get; }

    protected AppImageBase(IRuntime runtime)
    {
        Runtime = runtime;
    }

    public abstract Task<Result<IEnumerable<(ZafiroPath Path, IBlob Blob)>>> PayloadEntries();
}