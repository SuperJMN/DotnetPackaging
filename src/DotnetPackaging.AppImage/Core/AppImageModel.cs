using System.Diagnostics;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using Zafiro.Mixins;
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
        Debugger.Launch();
        
        yield return new RootedFile(ZafiroPath.Empty, new File("AppRun", Application.AppRun.Open));
        
        if (Application.Metadata.Icon.HasValue)
        {
            yield return new RootedFile(ZafiroPath.Empty, new File(".DirIcon", Application.Metadata.Icon.Value.Open));
            yield return new RootedFile(ZafiroPath.Empty, new File(Application.Metadata.AppName + ".png", Application.Metadata.Icon.Value.Open));
        }
        
        yield return new RootedFile(ZafiroPath.Empty, new File("App.desktop", () => Task.FromResult(Result.Success(DesktopFileContents(Application.Metadata).ToStream()))));
    }
    
    private string DesktopFileContents(Metadata metadata)
    {
        var textContent = $"""
                           [Desktop Entry]
                           Type=Application
                           Name={metadata.AppName}
                           StartupWMClass={metadata.StartupWmClass}
                           GenericName={metadata.AppName}
                           Comment={metadata.Comment}
                           Icon={Application.Metadata.AppName}
                           Terminal=false
                           Exec=$APPDIR/{Application.ExecutablePath}
                           Categories={metadata.Categories.Map(cats => string.Join(";", cats))};
                           Keywords={metadata.Keywords.Map(keywords => string.Join(";", keywords))};
                           """.FromCrLfToLf();

        return textContent;
    }
}