using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Globalization;
using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.AppImage.Kernel;
using DotnetPackaging.Console;
using Zafiro.DataModel;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using AppImage = DotnetPackaging.AppImage.AppImage;

class Program
{
    public static readonly FileSystem FileSystem = new();
    
    public static Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand();
        //rootCommand.AddCommand(DebCommand());
        rootCommand.AddCommand(AppImageCommand());
        
        return rootCommand.InvokeAsync(args);
    }

    static Command AppImageCommand()
    {
        var fromBuildDir = new Command("appimage", "Creates AppImage packages");
        //fromBuildDir.AddCommand(AppImageFromAppDirCommand());
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
        var version = new Option<string>("--version", "Version") { IsRequired = false };
        var homePage = new Option<Uri>("--homepage", "Home page of the application") { IsRequired = false };
        var license = new Option<string>("--license", "License of the application") { IsRequired = false };
        var screenshotUrls = new Option<IEnumerable<Uri>>("--screenshot-urls", "Screenshot URLs") { IsRequired = false };
        var summary = new Option<string>("--summary", "Summary. Short description that should not end in a dot.") { IsRequired = false };
        var appId = new Option<string>("--appId", "Application Id. Usually a Reverse DNS name like com.SomeCompany.SomeApplication") { IsRequired = false };
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
            appId);
        
        fromBuildDir.SetHandler(DoIt, buildDir, appImageFile, options);
        return fromBuildDir;
    }

    private static async Task DoIt(DirectoryInfo inputDir, FileInfo outputFile, Options options)
    {
        await AppImage.Create()
            .FromDirectory(new DotnetDir(FileSystem.DirectoryInfo.New(inputDir.FullName)))
            .Configure(setup => { })
            .Build()
            .Bind(x => AppImageMixin.ToData(x).Bind(async data =>
            {
                using (var fileSystemStream = outputFile.Open(FileMode.Create))
                {
                    return await data.DumpTo(fileSystemStream);
                }
            }));
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

internal class DirectoryInfoTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }
    
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string stringValue)
        {
            return Program.FileSystem.DirectoryInfo.New(stringValue);
        }

        return base.ConvertFrom(context, culture, value);
    }
}

internal class FileInfoTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }
    
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string stringValue)
        {
            return Program.FileSystem.FileInfo.New(stringValue);
        }

        return base.ConvertFrom(context, culture, value);
    }
}

//var rootCommand = new RootCommand();
//rootCommand.AddCommand(DebCommand());
//rootCommand.AddCommand(AppImageCommand());

//return await rootCommand.InvokeAsync(args);

//static async Task CreateDeb(DirectoryInfo contents, FileInfo debFile, FileInfo metadataFile)
//{
//    // TODO: Restore this
//    //var packagingDto = await metadataFile.ToDto();
//    //Log.Logger.Information("Creating {Deb} from {Contents}", debFile.FullName, contents.FullName);
//    //Log.Logger.Verbose("Metadata for {Deb} is set to {Metadata}", debFile.FullName, packagingDto);
//    //var packageDefinition = await packagingDto.ToModel();
//    //var result = await Create.Deb(packageDefinition, contents.FullName, debFile.FullName);

//    //result.WriteResult();
//}

//static Command DebCommand()
//{
//    var contentDir = new Option<DirectoryInfo>("--directory", "The input directory to create the package from") { IsRequired = true };
//    var metadata = new Option<FileInfo>("--metadata", "The metadata to include in the package") { IsRequired = true };
//    var debFile = new Option<FileInfo>("--output", "Output file (.deb)") { IsRequired = true };

//    var debCommand = new Command("deb", "Creates deb packages");
//    debCommand.AddOption(contentDir);
//    debCommand.AddOption(debFile);
//    debCommand.AddOption(metadata);

//    debCommand.SetHandler(CreateDeb, contentDir, debFile, metadata);
//    return debCommand;
//}