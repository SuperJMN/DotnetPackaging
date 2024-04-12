using System.Diagnostics;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;
using Directory = Zafiro.FileSystem.Lightweight.Directory;

namespace DotnetPackaging.AppImage.Core;

public class AppImageFactory
{
    public static AppImageBase FromAppDir(IDirectory appDir, IRuntime uriRuntime) => new AppDirBasedAppImage(uriRuntime, appDir);

    public static async Task<Result<AppImageBase>> FromBuildDir(IDirectory inputDir, SingleDirMetadata metadata, Func<Architecture, IRuntime> getRuntime)
    { 
        var execFile =
            await FileHelper.GetExecutables(inputDir)
                .Bind(tuples => tuples.TryFirst().ToResult("Could not find any executable in the input directory"))
                .Bind(exec => exec.File.Within(execStream => execStream.GetArchitecture()).Map(arch => (Arch: arch, Exec: exec)));

        if (execFile.IsFailure)
        {
            return Result.Failure<AppImageBase>("Could not find any executable file");
        }

        var firstExecutable = execFile.Value;
        var appName = Maybe.From(metadata.AppName).GetValueOrDefault(firstExecutable.Exec.File.Name.Replace(".Desktop", ""));
        IDirectory[] applicationContents =
        {
            new Directory(appName, inputDir.Files(), inputDir.Directories()),
        };
        
        var executablePath = "$APPDIR/" + appName + "/" + firstExecutable.Exec.File.Name;
        
        var desktopMetadata = new DesktopMetadata
        {
            Categories = metadata.Categories,
            Comment = metadata.Comment,
            ExecutablePath = executablePath,
            Keywords = metadata.Keywords,
            Name = metadata.AppName,
            StartupWmClass = metadata.StartupWmClass
        };

        return new AppImageModel(getRuntime(firstExecutable.Arch), new Application(applicationContents, Maybe<IIcon>.None, desktopMetadata, new DefaultScriptAppRun(executablePath)));
    }
}