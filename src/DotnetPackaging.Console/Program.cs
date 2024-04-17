using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.Deb.Client.Dtos;
using DotnetPackaging.Console;
using DotnetPackaging.Deb;
using DotnetPackaging.Deb.Archives;
using Serilog;
using File = System.IO.File;


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
    var mainCategory = new Option<MainCategory?>("--main-category", "Main category") { IsRequired = false, Arity = ArgumentArity.ZeroOrOne, };
    var additionalCategories = new Option<IEnumerable<AdditionalCategory>>("--additional-categories", "Additional categories") { IsRequired = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
    var keywords = new Option<IEnumerable<string>>("--keywords", "Keywords") { IsRequired = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
    var comment = new Option<string>("--comment", "Comment") { IsRequired = false };
    var iconOption = new Option<IIcon>("--icon", result =>
    {
        return GetIcon(result);

        IIcon GetIcon(SymbolResult argumentResult)
        {
            var iconPath = argumentResult.Tokens[0].Value;
            return new Icon(() => Task.FromResult(Result.Try(() => (Stream)File.OpenRead(iconPath))));
        }
    })
    {
        IsRequired = false, 
        Description = "Path to the application icon. When this options is not provided, the tool will look up for an image called 'AppImage.png'."
    };

    var fromBuildDir = new Command("from-build", "Creates AppImage from a directory with the contents. Everything is inferred. For .NET applications, this is usually the \"publish\" directory.");

    fromBuildDir.AddOption(buildDir);
    fromBuildDir.AddOption(appImageFile);
    fromBuildDir.AddOption(appName);
    fromBuildDir.AddOption(startupWmClass);
    fromBuildDir.AddOption(mainCategory);
    fromBuildDir.AddOption(keywords);
    fromBuildDir.AddOption(comment);
    fromBuildDir.AddOption(iconOption);
    fromBuildDir.AddOption(additionalCategories);

    fromBuildDir.SetHandler(
        (inputDir, outputFile, singleDirMetadata) => new FromSingleDirectory(new FileSystem()).Create(inputDir.FullName, outputFile.FullName, singleDirMetadata).WriteResult(), buildDir, appImageFile,
        new SingleDirOptionsBinder(appName, startupWmClass, keywords, comment, mainCategory, additionalCategories, iconOption));
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