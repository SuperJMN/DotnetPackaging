using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;
using Directory = Zafiro.FileSystem.Lightweight.Directory;

namespace DotnetPackaging.AppImage.Core;

public class AppImageFactory
{
    public static AppImageBase FromAppDir(IDirectory appDir, IRuntime uriRuntime) => new AppDirBasedAppImage(uriRuntime, appDir);

    public static async Task<Result<AppImageBase>> FromBuildDir(IDirectory inputDir, Maybe<SingleDirMetadata> metadata, Func<Architecture, IRuntime> getRuntime)
    { 
        var execFile =
            await FileHelper.GetExecutables(inputDir)
                .Bind(tuples => tuples.TryFirst().ToResult("Could not find any executable in the input directory"))
                .Bind(exec => exec.File.Within(execStream => execStream.GetArchitecture()).Map(arch => (Arch: arch, Exec: exec)));

        if (execFile.IsFailure)
        {
            return Result.Failure<AppImageBase>("Could not find any executable file");
        }

        var executable = execFile.Value;
        var appName = metadata.Bind(x => x.AppName).GetValueOrDefault(executable.Exec.File.Name.Replace(".Desktop", ""));
        
        var executablePath = "$APPDIR/" + appName + "/" + executable.Exec.File.Name;
        
        var desktopMetadata = metadata.Map(m => new DesktopMetadata
        {
            Categories = m.Categories,
            Comment = m.Comment,
            ExecutablePath = executablePath,
            Keywords = m.Keywords,
            Name = m.AppName,
            StartupWmClass = m.StartupWmClass
        });
        
        return new AppImageModel(getRuntime(executable.Arch), new Application(
            Maybe<IIcon>.None, 
            desktopMetadata, 
            new DefaultScriptAppRun(executablePath), 
            new Directory(appName, inputDir.Files(), inputDir.Directories())));
    }
}