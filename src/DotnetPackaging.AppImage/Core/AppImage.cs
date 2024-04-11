using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public class AppImageModel : AppImageBase
{
    public AppImageModel(IRuntime runtime, Application application) : base(runtime)
    {
        Application = application;
    }

    public Application Application { get; }
    public override Task<Result<IEnumerable<(ZafiroPath Path, IBlob Blob)>>> PayloadEntries() => GetPayload(Application);

    private async Task<Result<IEnumerable<(ZafiroPath Path, IBlob Blob)>>> GetPayload(Application application)
    {
        var entries = BasicEntries();
        var getBlobsListResult = await Task.WhenAll(application.Contents.Select(container => container.GetBlobsInTree(ZafiroPath.Empty)));
        var contentEntries = getBlobsListResult.Combine();
        var plain = contentEntries.Map(x => x.SelectMany(r => r));
        return plain.Map(ce => entries.Concat(ce));
    }

    private IEnumerable<(ZafiroPath Path, IBlob Blob)> BasicEntries()
    {
        yield return (ZafiroPath.Empty, new Blob("AppRun", Application.AppRun.StreamFactory));
        if (Application.Icon.HasValue)
        {
            yield return (ZafiroPath.Empty, new Blob(".AppIcon.png", Application.Icon.Value.StreamFactory));
        }
    }
}