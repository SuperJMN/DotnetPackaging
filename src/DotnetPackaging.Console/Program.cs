using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.AppImage.Kernel;
using DotnetPackaging.Console;
using DotnetPackaging.Deb;
using Serilog;
using Zafiro.DataModel;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using AppImage = DotnetPackaging.AppImage.AppImage;

class Program
{
    public static readonly FileSystem FileSystem = new();
    
    public static Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
        
        var rootCommand = new RootCommand();
        rootCommand.AddCommand(DebCommand());
        rootCommand.AddCommand(AppImageCommand());
        
        return rootCommand.InvokeAsync(args);
    }

    static Command AppImageCommand()
    {
        var fromBuildDir = new Command("appimage", "Creates AppImage packages");
        fromBuildDir.AddCommand(AppImageFromBuildDirCommand());
        return fromBuildDir;
    }
    
    static Command DebCommand()
    {
        var fromBuildDir = new Command("deb", "Creates AppImage packages");
        fromBuildDir.AddCommand(DebFromBuildDirCommand());
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
        var version = new Option<string>("--version", "Version") { IsRequired = false };
        var homePage = new Option<Uri>("--homepage", "Home page of the application") { IsRequired = false };
        var license = new Option<string>("--license", "License of the application") { IsRequired = false };
        var screenshotUrls = new Option<IEnumerable<Uri>>("--screenshot-urls", "Screenshot URLs") { IsRequired = false };
        var summary = new Option<string>("--summary", "Summary. Short description that should not end in a dot.") { IsRequired = false };
        var appId = new Option<string>("--appId", "Application Id. Usually a Reverse DNS name like com.SomeCompany.SomeApplication") { IsRequired = false };
        var executableName = new Option<string>("--executable-name", "Name of your application's executable") { IsRequired = false };
        var iconOption = new Option<IIcon>("--icon", result => GetIcon(result, result))
        {
            IsRequired = false,
            Description = "Path to the application icon. When this option is not provided, the tool will look up for an image called 'AppImage.png'."
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
        fromBuildDir.AddOption(version);
        fromBuildDir.AddOption(homePage);
        fromBuildDir.AddOption(license);
        fromBuildDir.AddOption(screenshotUrls);
        fromBuildDir.AddOption(summary);
        fromBuildDir.AddOption(appId);
        fromBuildDir.AddOption(executableName);

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
            executableName);
        
        fromBuildDir.SetHandler(CreateAppImage, buildDir, appImageFile, options);
        return fromBuildDir;
    }
    
    static Command DebFromBuildDirCommand()
    {
        var buildDir = new Option<DirectoryInfo>("--directory", "The input directory to create the package from") { IsRequired = true };
        var appImageFile = new Option<FileInfo>("--output", "Output file (.deb)") { IsRequired = true };
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
        var iconOption = new Option<IIcon>("--icon", result => GetIcon(result, result))
        {
            IsRequired = false,
            Description = "Path to the application icon. When this option is not provided, the tool will look up for an image called 'AppImage.png'."
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
        fromBuildDir.AddOption(version);
        fromBuildDir.AddOption(homePage);
        fromBuildDir.AddOption(license);
        fromBuildDir.AddOption(screenshotUrls);
        fromBuildDir.AddOption(summary);
        fromBuildDir.AddOption(appId);
        fromBuildDir.AddOption(executableName);

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
            executableName);
        
        fromBuildDir.SetHandler(CreateDeb, buildDir, appImageFile, options);
        return fromBuildDir;
    }

    private static Task CreateAppImage(DirectoryInfo inputDir, FileInfo outputFile, Options options)
    {
        return AppImage.From()
            .Directory(new DotnetDir(FileSystem.DirectoryInfo.New(inputDir.FullName)))
            .Configure(configuration => configuration.From(options))
            .Build()
            .Bind(x => x.ToData().Bind(async data =>
            {
                await using var fileSystemStream = outputFile.Open(FileMode.Create);
                return await data.DumpTo(fileSystemStream);
            }))
            .WriteResult();
    }
    
    private static Task CreateDeb(DirectoryInfo inputDir, FileInfo outputFile, Options options)
    {
        return DebFile.From()
            .Directory(new DotnetDir(FileSystem.DirectoryInfo.New(inputDir.FullName)))
            .Configure(configuration => configuration.From(options))
            .Build()
            .Map(DotnetPackaging.Deb.Archives.Deb.DebMixin.ToData)
            .Bind(async data =>
            {
                await using var fileSystemStream = outputFile.Open(FileMode.Create);
                return await data.DumpTo(fileSystemStream);
            })
            .WriteResult();
    }

    private static IIcon GetIcon(SymbolResult argumentResult, ArgumentResult result)
    {
        var iconPath = argumentResult.Tokens[0].Value;
        var icon = Icon.FromData(new FileInfoData(FileSystem.FileInfo.New(iconPath))).Result;
        if (icon.IsFailure)
        {
            result.ErrorMessage = $"Invalid icon '{iconPath}': {icon.Error}";
        }
                
        return null;
    }
}