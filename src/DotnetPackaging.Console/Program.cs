using System.CommandLine;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Deb.Client.Dtos;
using DotnetPackaging.Console;
using DotnetPackaging.Deb;
using DotnetPackaging.Deb.Archives;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

Log.Information("Executing...");

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

    result.WriteResult();
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
    var appName = new Option<string>("--application-name", "Application name") { IsRequired = false };
    var startupWmClass = new Option<string>("--wm-class", "Startup WM Class") { IsRequired = false };
    var categories = new Option<List<string>>("--categories", "Categories") { IsRequired = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
    var keywords = new Option<List<string>>("--keywords", "Categories") { IsRequired = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
    var comment = new Option<string>("--comment", "Comment") { IsRequired = false };

    var fromBuildDir = new Command("from-build", "Creates AppImage from a directory with the contents. Everything is inferred. For .NET applications, this is usually the \"publish\" directory.");

    fromBuildDir.AddOption(buildDir);
    fromBuildDir.AddOption(appImageFile);
    fromBuildDir.AddOption(appName);
    fromBuildDir.AddOption(startupWmClass);
    fromBuildDir.AddOption(categories);
    fromBuildDir.AddOption(keywords);
    fromBuildDir.AddOption(comment);

    fromBuildDir.SetHandler((inputDir, outputFile, singleDirMetadata) => new FromSingleDirectory(new FileSystem()).Create(inputDir.FullName, outputFile.FullName, Maybe.From(singleDirMetadata)).WriteResult(), buildDir, appImageFile, new SingleDirMetadataBinder(appName, startupWmClass, keywords, comment, categories));
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

    fromBuildDir.SetHandler((appDir, outputFile, architecture) => new FromAppDir(new FileSystem()).Create(appDir.FullName, outputFile.FullName, architecture).WriteResult(), buildDir, appImageFile, architecture);
    return fromBuildDir;
}