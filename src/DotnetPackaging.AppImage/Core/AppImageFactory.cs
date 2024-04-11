using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using Directory = Zafiro.FileSystem.Lightweight.Directory;

namespace DotnetPackaging.AppImage.Core;

public class AppImageFactory
{
    public static AppImageBase FromAppDir(IDirectory appDir, IRuntime uriRuntime) => new AppDirBasedAppImage(uriRuntime, appDir);

    public static async Task<Result<AppImageBase>> FromBuildDir(IDirectory buildDir, Maybe<DesktopMetadata> desktopMetadataOverride, Func<Architecture, IRuntime> getRuntime)
    {
        var firstExecutableResult = GetExecutable(buildDir).Bind(exec => { return exec.Blob.Within(execStream => execStream.GetArchitecture()).Map(arch => (Arch: arch, Exec: exec)); });

        var result = await firstExecutableResult
            .Bind(execWithArch =>
            {
                return CreateApplicationFromBuildDirectory(buildDir, execWithArch.Exec, desktopMetadataOverride)
                    .Map(application => (AppImageBase) new AppImageModel(getRuntime(execWithArch.Arch), application));
            });

        return result;
    }

    private static Task<Result<(ZafiroPath Path, IFile Blob)>> GetExecutable(IDirectory buildDirectory)
    {
        return buildDirectory.GetFilesInTree(ZafiroPath.Empty)
            .Bind(ToExecutableEntries)
            .Bind(x => x.TryFirst(file => file.IsExec).Select(tuple => (tuple.Path, tuple.Blob)).ToResult("Could not find any executable file"));
    }

    private static Task<Result<Application>> CreateApplicationFromBuildDirectory(IDirectory buildDir, (ZafiroPath Path, IFile Blob) firstExecutable, Maybe<DesktopMetadata> desktopMetadataOverride)
    {
        var allEntries = buildDir.GetFilesInTree(ZafiroPath.Empty).Bind(ToExecutableEntries);

        var maybeIconResult = allEntries.Map(x => x.TryFirst(file => file.Path.ToString() == "AppImage.png").Map(f => (IIcon) new Icon(f.Blob)));
        var appName = ((ZafiroPath)firstExecutable.Blob.Name).NameWithoutExtension();
        var desktopMetadata = new DesktopMetadata
        {
            ExecutablePath = "$APPDIR/" + firstExecutable.Path,
            Categories = new List<string>(),
            Comment = "",
            Keywords = [],
            Name = appName,
            StartupWmClass = appName
        };

        return from content in allEntries
            from maybeIcon in maybeIconResult
            select new Application(
                new[] { new Directory(appName, buildDir.Files(), buildDir.Directories()) },
                maybeIcon,
                desktopMetadataOverride.GetValueOrDefault(desktopMetadata),
                new DefaultScriptAppRun(firstExecutable.Path));
    }

    private static Task<Result<IEnumerable<(bool IsExec, ZafiroPath Path, IFile Blob)>>> ToExecutableEntries(IEnumerable<(ZafiroPath Path, IFile Blob)> files)
    {
        return files.Select(x => x.IsExecutable().Map(isExec => (IsExec: isExec, x.Path, x.Blob))).Combine();
    }
}