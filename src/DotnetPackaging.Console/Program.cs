using System.CommandLine;
using System.CommandLine.Parsing;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Metadata;
using DotnetPackaging.Deb;
using Serilog;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.DataModel;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using Zafiro.FileSystem.Core;

namespace DotnetPackaging.Console;

static class Program
{
    private static readonly System.IO.Abstractions.FileSystem FileSystem = new();
    
    public static Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {Platform}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        
        var rootCommand = new RootCommand();

        var debCommand = CreateCommand("deb", ".deb", ".deb", CreateDeb);
        rootCommand.AddCommand(debCommand);

        var appImageCommand = CreateCommand("appimage", "AppImage", ".AppImage", CreateAppImage);
        // Add subcommands for AppImage
        AddAppImageSubcommands(appImageCommand);
        rootCommand.AddCommand(appImageCommand);
        
        return rootCommand.InvokeAsync(args);
    }

    private static Command CreateCommand(string commandName, string friendlyName, string extension, Func<DirectoryInfo, FileInfo, Options, Task> handler)
    {
        var buildDir = new Option<DirectoryInfo>("--directory", "The input directory to create the package from") { IsRequired = true };
        var appImageFile = new Option<FileInfo>("--output", $"Output file ({extension})") { IsRequired = true };
        var appName = new Option<string>("--application-name", "Application name") { IsRequired = false };
        var startupWmClass = new Option<string>("--wm-class", "Startup WM Class") { IsRequired = false };
        var mainCategory = new Option<MainCategory?>("--main-category", "Main category") { IsRequired = false, Arity = ArgumentArity.ZeroOrOne, };
        var additionalCategories = new Option<IEnumerable<AdditionalCategory>>("--additional-categories", "Additional categories") { IsRequired = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var keywords = new Option<IEnumerable<string>>("--keywords", "Keywords") { IsRequired = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var comment = new Option<string>("--comment", "Comment") { IsRequired = false };
        var version = new Option<string>("--version", "Version") { IsRequired = false };
        var homePage = new Option<Uri>("--homepage", "Home page of the application") { IsRequired = false };
        var license = new Option<string>("--license", "License of the application") { IsRequired = false };
        var screenshotUrls = new Option<IEnumerable<Uri>>("--screenshot-urls", "Screenshot URLs") { IsRequired = false };
        var summary = new Option<string>("--summary", "Summary. Short description that should not end in a dot.") { IsRequired = false };
        var appId = new Option<string>("--appId", "Application Id. Usually a Reverse DNS name like com.SomeCompany.SomeApplication") { IsRequired = false };
        var executableName = new Option<string>("--executable-name", "Name of your application's executable") { IsRequired = false };
        var isTerminal = new Option<bool>("--is-terminal", "Indicates whether your application is a terminal application") { IsRequired = false };
        var iconOption = new Option<IIcon?>("--icon", GetIcon )
        {
            IsRequired = false,
            Description = "Path to the application icon"
        };

        var fromBuildDir = new Command(commandName, $"Creates {friendlyName} from a directory with the contents. Everything is inferred. For .NET applications, this is usually the \"publish\" directory.");

        fromBuildDir.AddOption(buildDir);
        fromBuildDir.AddOption(appImageFile);
        fromBuildDir.AddOption(appName);
        fromBuildDir.AddOption(startupWmClass);
        fromBuildDir.AddOption(mainCategory);
        fromBuildDir.AddOption(keywords);
        fromBuildDir.AddOption(comment);
        fromBuildDir.AddOption(iconOption);
        fromBuildDir.AddOption(additionalCategories);
        fromBuildDir.AddOption(version);
        fromBuildDir.AddOption(homePage);
        fromBuildDir.AddOption(license);
        fromBuildDir.AddOption(screenshotUrls);
        fromBuildDir.AddOption(summary);
        fromBuildDir.AddOption(appId);
        fromBuildDir.AddOption(executableName);
        fromBuildDir.AddOption(isTerminal);

        var options = new OptionsBinder(
            appName, 
            startupWmClass, 
            keywords, 
            comment, 
            mainCategory, 
            additionalCategories, 
            iconOption, 
            version, 
            homePage, 
            license, 
            screenshotUrls, 
            summary, 
            appId,
            executableName, 
            isTerminal);
        
        fromBuildDir.SetHandler(handler, buildDir, appImageFile, options);
        return fromBuildDir;
    }

    private static Task CreateAppImage(DirectoryInfo inputDir, FileInfo outputFile, Options options)
    {
        // Wrap the input directory as a DivineBytes container (no temp copies)
        var dirInfo = FileSystem.DirectoryInfo.New(inputDir.FullName);
        var container = new DirectoryContainer(dirInfo);
        var root = container.AsRoot();

        var metadata = BuildAppImageMetadata(options, inputDir);
        var factory = new AppImageFactory();

        return factory.Create(root, metadata)
            .Bind(x => x.ToByteSource())
            .Bind(source => source.WriteTo(outputFile.FullName))
            .WriteResult();
    }

    private static Maybe<IEnumerable<string>> BuildCategories(Options options)
    {
        var list = new List<string>();
        if (options.MainCategory.HasValue) list.Add(options.MainCategory.Value.ToString());
        if (options.AdditionalCategories.HasValue) list.AddRange(options.AdditionalCategories.Value.Select(x => x.ToString()));
        return list.Any() ? list : Maybe<IEnumerable<string>>.None;
    }

    private static AppImageMetadata BuildAppImageMetadata(Options options, DirectoryInfo contextDir)
    {
        var appName = options.Name.GetValueOrDefault(contextDir.Name);
        var packageName = appName.ToLowerInvariant().Replace(" ", "").Replace("-", "");
        var appId = options.Id.GetValueOrDefault($"com.{packageName}");

        return new AppImageMetadata(appId, appName, packageName)
        {
            Summary = options.Summary,
            Comment = options.Comment,
            Description = options.Comment, // use comment if no separate description is provided
            Version = options.Version,
            Homepage = options.HomePage.Map(u => u.ToString()),
            ProjectLicense = options.License,
            Keywords = options.Keywords,
            StartupWmClass = options.StartupWmClass,
            IsTerminal = options.IsTerminal.GetValueOrDefault(false),
            Categories = BuildCategories(options)
        };
    }

    private static void AddAppImageSubcommands(Command appImageCommand)
    {
        // Common options for metadata
        var appName = new Option<string>("--application-name", "Application name") { IsRequired = false };
        var startupWmClass = new Option<string>("--wm-class", "Startup WM Class") { IsRequired = false };
        var mainCategory = new Option<MainCategory?>("--main-category", "Main category") { IsRequired = false, Arity = ArgumentArity.ZeroOrOne };
        var additionalCategories = new Option<IEnumerable<AdditionalCategory>>("--additional-categories", "Additional categories") { IsRequired = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var keywords = new Option<IEnumerable<string>>("--keywords", "Keywords") { IsRequired = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var comment = new Option<string>("--comment", "Comment") { IsRequired = false };
        var version = new Option<string>("--version", "Version") { IsRequired = false };
        var homePage = new Option<Uri>("--homepage", "Home page of the application") { IsRequired = false };
        var license = new Option<string>("--license", "License of the application") { IsRequired = false };
        var screenshotUrls = new Option<IEnumerable<Uri>>("--screenshot-urls", "Screenshot URLs") { IsRequired = false };
        var summary = new Option<string>("--summary", "Summary. Short description that should not end in a dot.") { IsRequired = false };
        var appId = new Option<string>("--appId", "Application Id. Usually a Reverse DNS name like com.SomeCompany.SomeApplication") { IsRequired = false };
        var executableName = new Option<string>("--executable-name", "Name of your application's executable") { IsRequired = false };
        var isTerminal = new Option<bool>("--is-terminal", "Indicates whether your application is a terminal application") { IsRequired = false };
        var iconOption = new Option<IIcon?>("--icon", GetIcon )
        {
            IsRequired = false,
            Description = "Path to the application icon"
        };

        var binder = new OptionsBinder(
            appName,
            startupWmClass,
            keywords,
            comment,
            mainCategory,
            additionalCategories,
            iconOption,
            version,
            homePage,
            license,
            screenshotUrls,
            summary,
            appId,
            executableName,
            isTerminal);

        // appimage appdir
        var inputDir = new Option<DirectoryInfo>("--directory", "The input directory (publish output)") { IsRequired = true };
        var outputDir = new Option<DirectoryInfo>("--output-dir", "Destination directory for the AppDir") { IsRequired = true };
        var appDirCmd = new Command("appdir", "Creates an AppDir from a directory (does not package an .AppImage). For .NET apps, pass the publish directory.");
        appDirCmd.AddOption(inputDir);
        appDirCmd.AddOption(outputDir);
        appDirCmd.AddOption(appName);
        appDirCmd.AddOption(startupWmClass);
        appDirCmd.AddOption(mainCategory);
        appDirCmd.AddOption(additionalCategories);
        appDirCmd.AddOption(keywords);
        appDirCmd.AddOption(comment);
        appDirCmd.AddOption(version);
        appDirCmd.AddOption(homePage);
        appDirCmd.AddOption(license);
        appDirCmd.AddOption(screenshotUrls);
        appDirCmd.AddOption(summary);
        appDirCmd.AddOption(appId);
        appDirCmd.AddOption(executableName);
        appDirCmd.AddOption(isTerminal);
        appDirCmd.AddOption(iconOption);
        appDirCmd.SetHandler(CreateAppDir, inputDir, outputDir, binder);

        // appimage from-appdir
        var appDirPath = new Option<DirectoryInfo>("--directory", "The AppDir directory to package") { IsRequired = true };
        var outputFile = new Option<FileInfo>("--output", "Output .AppImage file") { IsRequired = true };
        var execRel = new Option<string>("--executable-relative-path", "Executable inside the AppDir (relative), e.g., usr/bin/MyApp") { IsRequired = false };
        var fromAppDirCmd = new Command("from-appdir", "Creates an AppImage from an existing AppDir directory.");
        fromAppDirCmd.AddOption(appDirPath);
        fromAppDirCmd.AddOption(outputFile);
        fromAppDirCmd.AddOption(execRel);
        fromAppDirCmd.AddOption(appName);
        fromAppDirCmd.AddOption(startupWmClass);
        fromAppDirCmd.AddOption(mainCategory);
        fromAppDirCmd.AddOption(additionalCategories);
        fromAppDirCmd.AddOption(keywords);
        fromAppDirCmd.AddOption(comment);
        fromAppDirCmd.AddOption(version);
        fromAppDirCmd.AddOption(homePage);
        fromAppDirCmd.AddOption(license);
        fromAppDirCmd.AddOption(screenshotUrls);
        fromAppDirCmd.AddOption(summary);
        fromAppDirCmd.AddOption(appId);
        fromAppDirCmd.AddOption(executableName);
        fromAppDirCmd.AddOption(isTerminal);
        fromAppDirCmd.AddOption(iconOption);
        fromAppDirCmd.SetHandler(CreateAppImageFromAppDir, appDirPath, outputFile, execRel, binder);

        appImageCommand.AddCommand(appDirCmd);
        appImageCommand.AddCommand(fromAppDirCmd);
    }

    private static Task CreateAppDir(DirectoryInfo inputDir, DirectoryInfo outputDir, Options options)
    {
        var dirInfo = FileSystem.DirectoryInfo.New(inputDir.FullName);
        var container = new DirectoryContainer(dirInfo);
        var root = container.AsRoot();

        var metadata = BuildAppImageMetadata(options, inputDir);
        var factory = new AppImageFactory();

        return factory.BuildAppDir(root, metadata)
            .Bind(rootDir => rootDir.AsContainer().WriteTo(outputDir.FullName))
            .WriteResult();
    }

    private static Task CreateAppImageFromAppDir(DirectoryInfo appDir, FileInfo outputFile, string? executableRelativePath, Options options)
    {
        var dirInfo = FileSystem.DirectoryInfo.New(appDir.FullName);
        var container = new DirectoryContainer(dirInfo);

        var metadata = BuildAppImageMetadata(options, appDir);
        var factory = new AppImageFactory();

        return factory.CreateFromAppDir(container, metadata, executableRelativePath, null)
            .Bind(x => x.ToByteSource())
            .Bind(source => source.WriteTo(outputFile.FullName))
            .WriteResult();
    }

    private static Task CreateDeb(DirectoryInfo inputDir, FileInfo outputFile, Options options)
    {
        return new Zafiro.FileSystem.Local.Directory(FileSystem.DirectoryInfo.New(inputDir.FullName))
            .ToDirectory()
            .Bind(directory => DebFile.From()
                .Directory(directory)
                .Configure(configuration => configuration.From(options))
                .Build()
                .Map(Deb.Archives.Deb.DebMixin.ToData)
                .Bind(async data =>
                {
                    await using var fileSystemStream = outputFile.Open(FileMode.Create);
                    return await data.DumpTo(fileSystemStream);
                }))
            .WriteResult();
    }

    private static IIcon? GetIcon(ArgumentResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return null;
        }

        var iconPath = result.Tokens[0].Value;
        var dataResult = Data.FromFileInfo(FileSystem.FileInfo.New(iconPath));
        if (dataResult.IsFailure)
        {
            result.ErrorMessage = $"Invalid icon '{iconPath}': {dataResult.Error}";
        }

        // For now, do not eagerly parse the icon (async). We rely on auto-detection or later stages.
        return null;
    }
}
