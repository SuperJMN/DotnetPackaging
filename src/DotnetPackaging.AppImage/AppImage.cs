using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Model;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage;

public class AppImage
{
    public static async Task<Result<Model.AppImage>> FromBuildDirectory(DirectoryBlobContainer buildDirectory, Maybe<DesktopMetadata> desktopMetadataOverride)
    {
        var allBlobsResult = await buildDirectory.GetBlobsInTree(ZafiroPath.Empty)
            .Bind(blobs =>
            {
                return blobs.Select(x =>
                {
                    return x.Blob
                        .Within(stream => stream.IsElf())
                        .Map(isExec => (IsExec: isExec, x.Path, x.Blob));
                }).Combine();
            });

        var firstExecutableResult = allBlobsResult.Bind(x => x.TryFirst(file => file.IsExec).ToResult("Could not find any executable file"));
        var maybeIconResult = allBlobsResult.Map(x => x.TryFirst(file => file.Path.ToString() == "AppImage.png").Map(f => (IIcon)new Icon(f.Blob)));
        var architectureResult = firstExecutableResult.Map(result => result.Blob.Within(stream => stream.IsElf()));
        var desktopMetadataResult = firstExecutableResult.Map(firstExec => new DesktopMetadata()
        {
            ExecutablePath = "$APPDIR/" + firstExec.Path,
            Categories = new List<string>(),
            Comment = "",
            Keywords = [],
            Name = firstExec.Path.NameWithoutExtension(),
            StartupWmClass = firstExec.Path.NameWithoutExtension(),
        });

        return await from content in allBlobsResult
            from firstExecutable in firstExecutableResult
            from maybeIcon in maybeIconResult
            from architecture in architectureResult
            from desktopMetadata in desktopMetadataResult
            select new Application(
                buildDirectory, 
                maybeIcon, 
                desktopMetadataOverride.Or(desktopMetadata.AsMaybe()), 
                new DefaultScriptAppRun(firstExecutable.Path));
    }

    public static Task<Result<Model.AppImage>> FromAppDir(DirectoryBlobContainer appDir, Architecture architecture)
    {
        return CreateApplication(appDir).Map(application => new Model.AppImage(new UriRuntime(architecture), application));
    }

    private static async Task<Result<Application>> CreateApplication(IBlobContainer appFolder)
    {
        var application = await appFolder.GetBlobsInTree(ZafiroPath.Empty)
            .Map(files => files.ToDictionary(x => x.Path, x => x.Blob))
            .Bind(async fileDict =>
            {
                var appRunResult = fileDict.TryFind("AppRun").Map(blob => new StreamAppRun(blob)).ToResult("Could not locate AppRun file in AppDir");
                var maybeIcon = fileDict.TryFind("AppIcon.png").Map(blob => (IIcon)new Icon(blob));
                var maybeDesktop = await fileDict.TryFind("App.desktop").Map(blob => blob.StreamFactory.FromStreamFactory());
                return appRunResult.Map(appRun => new Application(appFolder, maybeIcon, maybeDesktop, appRun));
            });

        return application;
    }
}