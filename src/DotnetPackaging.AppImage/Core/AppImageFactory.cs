using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using Serilog;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using Directory = Zafiro.FileSystem.Lightweight.Directory;

namespace DotnetPackaging.AppImage.Core;

public class AppImageFactory
{
    public static AppImageBase FromAppDir(IDirectory appDir, IRuntime uriRuntime) => new AppDirBasedAppImage(uriRuntime, appDir);

    public static async Task<Result<AppImageBase>> FromBuildDir(IDirectory inputDir, Options options, Func<Architecture, IRuntime> getRuntime)
    { 
        var execFile =
            await FileHelper.GetExecutables(inputDir)
                .Bind(tuples => tuples.TryFirst().ToResult("Could not find any executable in the input directory"))
                .Bind(exec => exec.File.Within(execStream => execStream.GetArchitecture()).Map(arch => (Arch: arch, Exec: exec)));

        if (execFile.IsFailure)
        {
            return Result.Failure<AppImageBase>("Could not find any executable file");
        }

        if (options.MainCategory.HasNoValue && options.AdditionalCategories.HasValue)
        {
            return Result.Failure<AppImageBase>("You specified additional categories, but a main category hasn't been specified. Please, use a main category");
        }

        var executable = execFile.Value;

        var appName = options.AppName.GetValueOrDefault(() => executable.Exec.File.Name.Replace(".Desktop", ""));
        
        var metadata = new Metadata
        {
            Icon = await options.Icon.Or(() => GetIconFromBuildDir(inputDir)),
            Version = options.Version,
            AppName = appName,
            Keywords = options.Keywords,
            Comment = options.Comment,
            Categories = options.MainCategory.Map(main => new Categories(main, options.AdditionalCategories.GetValueOrDefault(new List<AdditionalCategory>()).ToArray())),
            StartupWmClass = options.StartupWmClass.Or(appName),
            HomePage = options.HomePage,
            License = options.License,
            ScreenshotUrls = options.ScreenshotUrls,
            Summary = options.Summary,
            AppId = options.AppId,
        };
        
        Log.Information("Executable file: {Executable}, Architecture: {ExeArchitecture}", executable.Exec.FullPath(), executable.Arch);
        var executablePath = "$APPDIR/" + options.AppName + "/" + executable.Exec.File.Name;
        Log.Information("AppName: {AppName}", options.AppName);
        
        return new AppImageModel(getRuntime(executable.Arch), new Application(
            metadata, 
            ((ZafiroPath)metadata.AppName).Combine(executable.Exec.FullPath()),
            new DefaultScriptAppRun(executablePath), 
            new Directory(metadata.AppName, inputDir.Files(), inputDir.Directories())));
    }

    private static async Task<Maybe<IIcon>> GetIconFromBuildDir(IDirectory inputDir)
    {
        var result = await inputDir.Files().Map(files => files.TryFirst(file => file.Name == "AppImage.png").Map(file =>
        {
            var icon = Icon.FromDataStream(file);
            Log.Information("Using icon from 'AppImage.png' defined in input directory");
            return icon;
        }));

        return result.AsMaybe().Bind(x => x);
    }
}