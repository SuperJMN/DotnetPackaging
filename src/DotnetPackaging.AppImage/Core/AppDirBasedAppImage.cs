using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public class AppDirBasedAppImage : AppImageBase
{
    private readonly IDirectory container;

    public AppDirBasedAppImage(IRuntime runtime, IDirectory container) : base(runtime)
    {
        this.container = container;
    }

    public override Task<Result<IEnumerable<RootedFile>>> PayloadEntries()
    {
        return container.GetFilesInTree(ZafiroPath.Empty);
    }
}