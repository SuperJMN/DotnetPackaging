using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Model;

public abstract class AppImageBase
{
    public IRuntime Runtime { get; }

    public AppImageBase(IRuntime runtime)
    {
        Runtime = runtime;
    }

    public abstract Task<Result<IEnumerable<(ZafiroPath Path, IBlob Blob)>>> PayloadEntries();
}