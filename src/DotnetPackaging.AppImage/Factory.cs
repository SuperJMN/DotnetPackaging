using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.ValueTasks;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Model;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage;

public class Factory
{
    public static Task<Result<Model.AppImage>> From(DirectoryBlobContainer appFolder, Architecture architecture)
    {
        return CreateApplication(appFolder).Map(application => new Model.AppImage(new UriRuntime(architecture), application));
    }

    private static async Task<Result<Application>> CreateApplication(IBlobContainer appFolder)
    {
        var application = await appFolder.GetBlobsInTree(ZafiroPath.Empty)
            .Map(files => files.ToDictionary(x => x.Path, x => x.Blob))
            .Bind(fileDict =>
            {
                var appRunResult = fileDict.TryFind("AppRun").Map(blob => new StreamAppRun(blob.StreamFactory)).ToResult("Could not locate AppRun file in AppDir");
                var maybeIcon = fileDict.TryFind("AppIcon.png").Map(blob => (IIcon) new Icon(blob.StreamFactory)).GetValueOrDefault(() => new DefaultIcon());
                //var desktopMetadata = files["AppIcon.png"];
                return appRunResult.Map(appRun => new Application(appFolder, maybeIcon, Maybe<DesktopMetadata>.None, appRun));
            });

        return application;
    }
}