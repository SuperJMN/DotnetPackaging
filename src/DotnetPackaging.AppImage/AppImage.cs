using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Model;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage;

public abstract class AppImageBase
{
    public IRuntime Runtime { get; }

    public AppImageBase(IRuntime runtime)
    {
        Runtime = runtime;
    }
    
    public abstract Task<Result<IEnumerable<(ZafiroPath Path, IBlob Blob)>>> PayloadEntries();
}

public class RawAppImage : AppImageBase
{
    private readonly IBlobContainer container;
    
    public RawAppImage(IRuntime runtime, IBlobContainer container) : base(runtime)
    {
        this.container = container;
    }

    public override Task<Result<IEnumerable<(ZafiroPath Path, IBlob Blob)>>> PayloadEntries()
    {
        return container.GetBlobsInTree(ZafiroPath.Empty);
    }
}

public class AppImage
{
    private static async Task<Result<Application>> CreateApplicationFromBuildDirectory(IBlobContainer buildDir, (ZafiroPath Path, IBlob Blob) firstExecutable, Maybe<DesktopMetadata> desktopMetadataOverride)
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
        
        var applicationFromBuildDirectory = from content in allEntries
            from maybeIcon in maybeIconResult
            select new Application(
                new []{ new BlobContainer(appName, buildDir.Blobs(), buildDir.Children()) }, 
                maybeIcon, 
                desktopMetadataOverride.GetValueOrDefault(desktopMetadata), 
                new DefaultScriptAppRun(firstExecutable.Path));
        
        return await applicationFromBuildDirectory;
    }

    private static Task<Result<IEnumerable<(bool IsExec, ZafiroPath Path, IBlob Blob)>>> ToExecutableEntries(IEnumerable<(ZafiroPath Path, IBlob Blob)> files)
    {
        return files.Select(x => x.IsExecutable().Map(isExec => (IsExec: isExec, x.Path, x.Blob))).Combine();
    }

    public static AppImageBase FromAppDir(DirectoryBlobContainer appDir, Architecture architecture)
    {
        return new RawAppImage(new UriRuntime(architecture), appDir);
    }

    public static async Task<Result<Model.CustomAppImage>> FromBuildDir(DirectoryBlobContainer buildDir, Maybe<DesktopMetadata> desktopMetadataOverride)
    {
        var firstExecutableResult = GetExecutable(buildDir).Bind(exec =>
        {
            return exec.Blob.Within(execStream => execStream.GetArchitecture()).Map(arch => (Arch: arch, Exec: exec));
        });

        var result = await firstExecutableResult
            .Bind(execWithArch =>
            {
                return CreateApplicationFromBuildDirectory(buildDir, execWithArch.Exec, desktopMetadataOverride)
                        .Map(application => new Model.CustomAppImage(new UriRuntime(execWithArch.Arch), application));
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