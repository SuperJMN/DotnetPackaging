﻿using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.ValueTasks;
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

        var executable = execFile.Value;

        var appName = options.AppName.GetValueOrDefault(() => executable.Exec.File.Name.Replace(".Desktop", ""));
        
        var metadata = new Metadata
        {
            Icon = await options.Icon.Or(() => GetIconFromBuildDir(inputDir)),
            AppName = appName,
            Keywords = options.Keywords,
            Comment = options.Comment,
            Categories = options.Categories,
            StartupWmClass = options.StartupWmClass.Or(appName)
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
            var icon = (IIcon)new Icon(file.Open);
            Log.Information("Using icon from 'AppImage.png' defined in input directory");
            return icon;
        }));

        return result.AsMaybe().Bind(x => x);
    }
}