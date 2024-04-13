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
    
    public override Task<Result<IEnumerable<RootedFile>>> PayloadEntries() => GetPayload(Application);

    private async Task<Result<IEnumerable<RootedFile>>> GetPayload(Application application)
    {
        var entries = BasicEntries();
        var getBlobsListResult = await Task.WhenAll(application.Contents.Select(container => container.GetFilesInTree(container.Name)));
        var contentEntries = getBlobsListResult.Combine();
        var plain = contentEntries.Map(x => x.SelectMany(r => r));
        return plain.Map(ce => entries.Concat(ce));
    }

    private IEnumerable<RootedFile> BasicEntries()
    {
        yield return new RootedFile(ZafiroPath.Empty, new File("AppRun", Application.AppRun.Open));
        if (Application.Icon.HasValue)
        {
            yield return new RootedFile(ZafiroPath.Empty, new File(".AppIcon.png", Application.Icon.Value.Open));
        }

        if (Application.DesktopMetadata.HasValue)
        {
            yield return new RootedFile(ZafiroPath.Empty, new File("App.desktop", Application.DesktopMetadata.Value.ToStreamFactory()));
        }
    }
}