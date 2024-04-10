using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Model;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage;

public class AppImage
{
    private static async Task<Result<Application>> CreateApplicationFromBuildDirectory(IBlobContainer buildDir, (ZafiroPath Path, IBlob Blob) firstExecutable, Maybe<DesktopMetadata> desktopMetadataOverride)
    {
        var allEntries = buildDir.GetBlobsInTree(ZafiroPath.Empty).Bind(Complete);

        var maybeIconResult = allEntries.Map(x => x.TryFirst(file => file.Path.ToString() == "AppImage.png").Map(f => (IIcon)new Icon(f.Blob)));
        var architectureResult = await firstExecutable.Blob.Within(stream => stream.IsExecutable());
        var desktopMetadata = new DesktopMetadata()
        {
            ExecutablePath = "$APPDIR/" + firstExecutable.Path,
            Categories = new List<string>(),
            Comment = "",
            Keywords = [],
            Name = firstExecutable.Path.NameWithoutExtension(),
            StartupWmClass = firstExecutable.Path.NameWithoutExtension(),
        };
        
        var applicationFromBuildDirectory = from content in allEntries
            from maybeIcon in maybeIconResult
            from architecture in architectureResult
            select new Application(
                new BlobContainer("", buildDir.Blobs(), buildDir.Children()), 
                maybeIcon, 
                desktopMetadataOverride.GetValueOrDefault(desktopMetadata), 
                new DefaultScriptAppRun(firstExecutable.Path));
        
        return await applicationFromBuildDirectory;
    }

    private static Task<Result<IEnumerable<(bool IsExec, ZafiroPath Path, IBlob Blob)>>> Complete(IEnumerable<(ZafiroPath Path, IBlob Blob)> files)
    {
        return files.Select(x =>
        {
            return x.Blob
                .Within(stream => stream.IsExecutable())
                .Map(isExec => (IsExec: isExec, x.Path, x.Blob));
        }).Combine();
    }

    public static Task<Result<Model.AppImage>> FromAppDir(DirectoryBlobContainer appDir, Architecture architecture)
    {
        return CreateApplicationFromAppDir(appDir).Map(application => new Model.AppImage(new UriRuntime(architecture), application));
    }

    public static async Task<Result<Model.AppImage>> FromBuildDir(DirectoryBlobContainer buildDir, Maybe<DesktopMetadata> desktopMetadataOverride)
    {
        var firstExecutableResult = GetExecutable(buildDir).Bind(exec =>
        {
            return exec.Blob.Within(execStream => execStream.GetArchitecture()).Map(arch => (Arch: arch, Exec: exec));
        });

        var result = await firstExecutableResult
            .Bind(execWithArch =>
            {
                return CreateApplicationFromBuildDirectory(buildDir, execWithArch.Exec, desktopMetadataOverride)
                        .Map(application => new Model.AppImage(new UriRuntime(execWithArch.Arch), application));
            });
        
        return result;
    }

    private static async Task<Result<Application>> CreateApplicationFromAppDir(IBlobContainer appFolder)
    {
        var application = await appFolder.GetBlobsInTree(ZafiroPath.Empty)
            .Map(files => files.ToDictionary(x => x.Path, x => x.Blob))
            .Bind(async fileDict =>
            {
                var appRunResult = fileDict.TryFind("AppRun").Map(blob => new StreamAppRun(blob)).ToResult("Could not locate AppRun file in AppDir");
                var maybeIcon = fileDict.TryFind("AppIcon.png").Map(blob => (IIcon)new Icon(blob));
                var maybeDesktop = await fileDict.TryFind("App.desktop").Map(blob => blob.StreamFactory.FromStreamFactory());
                return appRunResult
                    .Map(appRun => new Application(appFolder, maybeIcon, maybeDesktop, appRun));
            });

        return application;
    }

    public static async Task<Result<(ZafiroPath Path, IBlob Blob)>> GetExecutable(IBlobContainer buildDirectory)
    {
        var allBlobsResult = await buildDirectory.GetBlobsInTree(ZafiroPath.Empty)
            .Bind(blobs =>
            {
                return blobs.Select(x =>
                {
                    return x.Blob
                        .Within(stream => stream.IsExecutable().Map(isExec => isExec && x.Path.Extension() != "so" && x.Path.Name() != "createdump"))
                        .Map(isExec => (IsExec: isExec, x.Path, x.Blob));
                }).Combine();
            });

        return allBlobsResult.Bind(x => x.TryFirst(file => file.IsExec).Select(tuple => (tuple.Path, tuple.Blob)).ToResult("Could not find any executable file"));
    }
}