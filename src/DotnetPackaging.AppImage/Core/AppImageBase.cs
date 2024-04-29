using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public abstract class AppImageBase
{
    public IRuntime Runtime { get; }

    protected AppImageBase(IRuntime runtime)
    {
        Runtime = runtime;
    }

    public abstract Task<Result<IEnumerable<IRootedFile>>> PayloadEntries();
}