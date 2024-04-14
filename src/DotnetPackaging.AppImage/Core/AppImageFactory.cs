using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using Serilog;
using Zafiro.CSharpFunctionalExtensions;
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
        
        Log.Information("Executable file: {Executable}, Architecture: {ExeArchitecture}", executable.Exec.FullPath(), executable.Arch);
        
        var appName = metadata.Bind(x => x.AppName).GetValueOrDefault(executable.Exec.File.Name.Replace(".Desktop", ""));
        
        var executablePath = "$APPDIR/" + appName + "/" + executable.Exec.File.Name;
        
        var maybeDesktopMetadata = metadata.Map(m => new DesktopMetadata
        {
            Categories = m.Categories,
            Comment = m.Comment,
            ExecutablePath = executablePath,
            Keywords = m.Keywords,
            Name = m.AppName,
            Path = "$APPDIR/" + appName,
            StartupWmClass = m.StartupWmClass.Or(appName)
        });

        var maybeIcon = await metadata.Bind(x => x.Icon).Or(() => GetIconFromBuildDir(inputDir));
        
        Log.Information("AppName: {AppName}", appName);
        
        return new AppImageModel(getRuntime(executable.Arch), new Application(
            maybeIcon, 
            maybeDesktopMetadata, 
            new DefaultScriptAppRun(executablePath), 
            new Directory(appName, inputDir.Files(), inputDir.Directories())));
    }

    private static async Task<Maybe<IIcon>> GetIconFromBuildDir(IDirectory inputDir)
    {
        var result = await inputDir.Files().Map(files => files.TryFirst(file => file.Name == "AppImage.png").Map(file =>
        {
            var icon = (IIcon)new Icon(file.Open);
            Log.Information("Using icon from 'AppImage.png' defined in input directory");
            return icon;
        }));

        return result.AsMaybe().Bind(x => x);
    }
}