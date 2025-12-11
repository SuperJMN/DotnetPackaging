using System.CommandLine;
using Serilog;

namespace DotnetPackaging.Tool.Commands;

public static class CommandFactory
{
    public static Command CreateCommand(
        string commandName,
        string friendlyName,
        string extension,
        Func<DirectoryInfo, FileInfo, Options, ILogger, Task> handler,
        string? description = null,
        Option<bool>? defaultLayoutOption = null,
        params string[] aliases)
    {
        var buildDir = new Option<DirectoryInfo>("--directory")
        {
            Description = "Published application directory (for example: bin/Release/<tfm>/publish)",
            Required = true
        };
        var outputFileOption = new Option<FileInfo>("--output")
        {
            Description = $"Destination path for the generated {extension} file",
            Required = true
        };
        var appName = new Option<string>("--application-name")
        {
            Description = "Application name",
            Required = false
        };
        appName.Aliases.Add("--productName");
        appName.Aliases.Add("--appName");
        var startupWmClass = new Option<string>("--wm-class")
        {
            Description = "Startup WM Class",
            Required = false
        };
        var mainCategory = new Option<MainCategory?>("--main-category")
        {
            Description = "Main category",
            Required = false,
            Arity = ArgumentArity.ZeroOrOne,
        };
        var additionalCategories = new Option<IEnumerable<AdditionalCategory>>("--additional-categories")
        {
            Description = "Additional categories",
            Required = false,
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };
        var keywords = new Option<IEnumerable<string>>("--keywords")
        {
            Description = "Keywords",
            Required = false,
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };
        var comment = new Option<string>("--comment")
        {
            Description = "Comment",
            Required = false
        };
        var version = new Option<string>("--version")
        {
            Description = "Version",
            Required = false
        };
        var homePage = new Option<Uri>("--homepage")
        {
            Description = "Home page of the application",
            Required = false
        };
        var license = new Option<string>("--license")
        {
            Description = "License of the application",
            Required = false
        };
        var screenshotUrls = new Option<IEnumerable<Uri>>("--screenshot-urls")
        {
            Description = "Screenshot URLs",
            Required = false
        };
        var summary = new Option<string>("--summary")
        {
            Description = "Summary. Short description that should not end in a dot.",
            Required = false
        };
        var appId = new Option<string>("--appId")
        {
            Description = "Application Id. Usually a Reverse DNS name like com.SomeCompany.SomeApplication",
            Required = false
        };
        var executableName = new Option<string>("--executable-name")
        {
            Description = "Name of your application's executable",
            Required = false
        };
        var isTerminal = new Option<bool>("--is-terminal")
        {
            Description = "Indicates whether your application is a terminal application",
            Required = false
        };
        var iconOption = new Option<IIcon?>("--icon")
        {
            Required = false,
            Description = "Path to the application icon"
        };
        iconOption.CustomParser = OptionsBinder.GetIcon;

        var defaultDescription = description ??
                                 $"Create a {friendlyName} from a directory with the published application contents. Everything is inferred. For .NET apps this is usually the 'publish' directory.";
        var fromBuildDir = new Command(commandName, defaultDescription);

        foreach (var alias in aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                fromBuildDir.Aliases.Add(alias);
            }
        }

        fromBuildDir.Add(buildDir);
        fromBuildDir.Add(outputFileOption);
        fromBuildDir.Add(appName);
        fromBuildDir.Add(startupWmClass);
        fromBuildDir.Add(mainCategory);
        fromBuildDir.Add(keywords);
        fromBuildDir.Add(comment);
        fromBuildDir.Add(iconOption);
        fromBuildDir.Add(additionalCategories);
        fromBuildDir.Add(version);
        fromBuildDir.Add(homePage);
        fromBuildDir.Add(license);
        fromBuildDir.Add(screenshotUrls);
        fromBuildDir.Add(summary);
        fromBuildDir.Add(appId);
        fromBuildDir.Add(executableName);
        fromBuildDir.Add(isTerminal);
        if (defaultLayoutOption != null)
        {
            fromBuildDir.Add(defaultLayoutOption);
        }

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
            isTerminal,
            defaultLayoutOption);
        
        fromBuildDir.SetAction(async parseResult =>
        {
            var directory = parseResult.GetValue(buildDir)!;
            var output = parseResult.GetValue(outputFileOption)!;
            var opts = options.Bind(parseResult);
            await ExecutionWrapper.ExecuteWithLogging(commandName, output.FullName, logger => handler(directory, output, opts, logger));
        });
        return fromBuildDir;
    }
}
