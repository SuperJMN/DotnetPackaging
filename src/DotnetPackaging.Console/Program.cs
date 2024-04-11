using System.CommandLine;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.Client.Dtos;
using DotnetPackaging.Common;
using DotnetPackaging.Console;
using Serilog;
using Zafiro.FileSystem.Lightweight;
using FileMode = System.IO.FileMode;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var rootCommand = new RootCommand();
rootCommand.AddCommand(DebCommand());
rootCommand.AddCommand(AppImageCommand());

return await rootCommand.InvokeAsync(args);

static async Task CreateDeb(DirectoryInfo contents, FileInfo debFile, FileInfo metadataFile)
{
    var packagingDto = await metadataFile.ToDto();
    Log.Logger.Information("Creating {Deb} from {Contents}", debFile.FullName, contents.FullName);
    Log.Logger.Verbose("Metadata for {Deb} is set to {Metadata}", debFile.FullName, packagingDto);
    var packageDefinition = await packagingDto.ToModel();
    var result = await Create.Deb(packageDefinition, contents.FullName, debFile.FullName);

    result
        .Tap(() => Log.Information("Success"))
        .TapError(Log.Error);
}

static async Task CreateAppImageFromAppDir(DirectoryInfo contents, FileInfo debFile, Architecture architecture)
{
    var fs = new FileSystem();
    var directoryInfo = fs.DirectoryInfo.New(contents.FullName);
    var buildDir = new DirectorioIODirectory("", directoryInfo);
    var fileSystemStream = fs.File.Open(debFile.FullName, FileMode.Create);
    var result = AppImageWriter.Write(fileSystemStream, AppImageFactory.FromAppDir(buildDir, new UriRuntime(architecture)));

    await result
        .Tap(() => Log.Information("Success"))
        .TapError(Log.Error);
}



static Command DebCommand()
{
    var contentDir = new Option<DirectoryInfo>("--directory", "The input directory to create the package from") { IsRequired = true };
    var metadata = new Option<FileInfo>("--metadata", "The metadata to include in the package") { IsRequired = true };
    var debFile = new Option<FileInfo>("--output", "Output file (.deb)") { IsRequired = true };

    var debCommand = new Command("deb", "Creates deb packages");
    debCommand.AddOption(contentDir);
    debCommand.AddOption(debFile);
    debCommand.AddOption(metadata);

    debCommand.SetHandler(CreateDeb, contentDir, debFile, metadata);
    return debCommand;
}

static Command AppImageCommand()
{
    var fromBuildDir = new Command("appimage", "Creates AppImage packages");
    fromBuildDir.AddCommand(AppImageFromAppDirCommand());
    fromBuildDir.AddCommand(AppImageFromBuildDirCommand());
    return fromBuildDir;
}

static Command AppImageFromBuildDirCommand()
{
    var buildDir = new Option<DirectoryInfo>("--directory", "The input directory to create the package from") { IsRequired = true };
    var appImageFile = new Option<FileInfo>("--output", "Output file (.deb)") { IsRequired = true };
    var desktopFile = new Option<string>("--desktop-file", "Desktop file formatted as JSON") { IsRequired = false };
    
    var fromBuildDir = new Command("from-build", "Creates AppImage from a directory with the contents. Everything is inferred. For .NET applications, this is usually the \"publish\" directory.");
    fromBuildDir.AddOption(buildDir);
    fromBuildDir.AddOption(appImageFile);
    fromBuildDir.AddOption(desktopFile);

    fromBuildDir.SetHandler(new FromAppDir(new FileSystem()).Create, buildDir, appImageFile, desktopFile);
    return fromBuildDir;
}

static Command AppImageFromAppDirCommand()
{
    var buildDir = new Option<DirectoryInfo>("--directory", "The input directory to create the package from") { IsRequired = true };
    var appImageFile = new Option<FileInfo>("--output", "Output file (.deb)") { IsRequired = true };
    var architecture = new Option<Architecture>("--architecture", "Architecture of the target AppImage") { IsRequired = true };

    var fromBuildDir = new Command("from-appdir", "Creates AppImage from an AppDir.");
    fromBuildDir.AddOption(buildDir);
    fromBuildDir.AddOption(appImageFile);
    fromBuildDir.AddOption(architecture);
    
    fromBuildDir.SetHandler(CreateAppImageFromAppDir, buildDir, appImageFile, architecture);
    return fromBuildDir;
}