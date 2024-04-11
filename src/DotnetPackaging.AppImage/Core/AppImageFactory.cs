using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public class AppImageFactory
{
    private static Task<Result<Application>> CreateApplicationFromBuildDirectory(IBlobContainer buildDir, (ZafiroPath Path, IBlob Blob) firstExecutable, Maybe<DesktopMetadata> desktopMetadataOverride)
    {
        var allEntries = buildDir.GetBlobsInTree(ZafiroPath.Empty).Bind(ToExecutableEntries);

        var maybeIconResult = allEntries.Map(x => x.TryFirst(file => file.Path.ToString() == "AppImage.png").Map(f => (IIcon)new Icon(f.Blob)));
        var appName = firstExecutable.Path.NameWithoutExtension();
        var desktopMetadata = new DesktopMetadata()
        {
            ExecutablePath = "$APPDIR/" + firstExecutable.Path,
            Categories = new List<string>(),
            Comment = "",
            Keywords = [],
            Name = appName,
            StartupWmClass = appName,
        };

        return from content in allEntries
            from maybeIcon in maybeIconResult
            select new Application(
                new[] { new BlobContainer(appName, buildDir.Blobs(), buildDir.Children()) },
                maybeIcon,
                desktopMetadataOverride.GetValueOrDefault(desktopMetadata),
                new DefaultScriptAppRun(firstExecutable.Path));
    }

    private static Task<Result<IEnumerable<(bool IsExec, ZafiroPath Path, IBlob Blob)>>> ToExecutableEntries(IEnumerable<(ZafiroPath Path, IBlob Blob)> files)
    {
        return files.Select(x => x.IsExecutable().Map(isExec => (IsExec: isExec, x.Path, x.Blob))).Combine();
    }

    public static AppImageBase FromAppDir(IBlobContainer appDir, IRuntime uriRuntime)
    {
        return new AppDirBasedAppImage(uriRuntime, appDir);
    }

    public static async Task<Result<AppImageBase>> FromBuildDir(IBlobContainer buildDir, Maybe<DesktopMetadata> desktopMetadataOverride, Func<Architecture, IRuntime> getRuntime)
    {
        var firstExecutableResult = GetExecutable(buildDir).Bind(exec =>
        {
            return exec.Blob.Within(execStream => execStream.GetArchitecture()).Map(arch => (Arch: arch, Exec: exec));
        });

        var result = await firstExecutableResult
            .Bind(execWithArch =>
            {
                return CreateApplicationFromBuildDirectory(buildDir, execWithArch.Exec, desktopMetadataOverride)
                        .Map(application => (AppImageBase)new AppImageModel(getRuntime(execWithArch.Arch), application));
            });

        return result;
    }

    public static Task<Result<(ZafiroPath Path, IBlob Blob)>> GetExecutable(IBlobContainer buildDirectory)
    {
        return buildDirectory.GetBlobsInTree(ZafiroPath.Empty)
            .Bind(ToExecutableEntries)
            .Bind(x => x.TryFirst(file => file.IsExec).Select(tuple => (tuple.Path, tuple.Blob)).ToResult("Could not find any executable file"));
    }
}