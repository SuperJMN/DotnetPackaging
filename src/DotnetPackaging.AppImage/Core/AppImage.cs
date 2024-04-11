using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.AppImage.Core;

public class AppImageModel : AppImageBase
{
    public AppImageModel(IRuntime runtime, Application application) : base(runtime)
    {
        Application = application;
    }

    public Application Application { get; }
    public override Task<Result<IEnumerable<(ZafiroPath Path, IFile Blob)>>> PayloadEntries() => GetPayload(Application);

    private async Task<Result<IEnumerable<(ZafiroPath Path, IFile Blob)>>> GetPayload(Application application)
    {
        var entries = BasicEntries();
        var getBlobsListResult = await Task.WhenAll(application.Contents.Select(container => container.GetFilesInTree(ZafiroPath.Empty)));
        var contentEntries = getBlobsListResult.Combine();
        var plain = contentEntries.Map(x => x.SelectMany(r => r));
        return plain.Map(ce => entries.Concat(ce));
    }

    private IEnumerable<(ZafiroPath Path, IFile Blob)> BasicEntries()
    {
        yield return (ZafiroPath.Empty, new File("AppRun", Application.AppRun.Open));
        if (Application.Icon.HasValue)
        {
            yield return (ZafiroPath.Empty, new File(".AppIcon.png", Application.Icon.Value.Open));
        }
    }
}