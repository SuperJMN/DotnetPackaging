using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage;
using System.Runtime.InteropServices;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Metadata;
using DotnetPackaging.Deb;
using DotnetPackaging.Flatpak;
using DotnetPackaging.Rpm;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Zafiro.DataModel;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using Zafiro.FileSystem.Core;
using DotnetPackaging.Exe;
using System.Threading;

namespace DotnetPackaging.Tool;

static class Program
{
    private const string VerboseEnvVar = "DOTNETPACKAGING_VERBOSE";
    private const string LegacyVerboseEnvVar = "DOTNETPACKAGING_DEBUG";
    private static readonly System.IO.Abstractions.FileSystem FileSystem = new();
    
    public static async Task<int> Main(string[] args)
    {
        var verboseEnabled = IsVerboseRequested(args);
        SetVerboseEnvironment(verboseEnabled);

        var levelSwitch = new LoggingLevelSwitch(verboseEnabled ? LogEventLevel.Debug : LogEventLevel.Information);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Tool", "DotnetPackaging.Tool")
            .Enrich.WithProperty("Platform", "General")
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {Tool}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        Log.Information("dotnetpackaging started (verbose={Verbose})", verboseEnabled);
        
        var rootCommand = new RootCommand
        {
            Description = "Package published .NET applications into Linux-friendly formats.\n\n" +
                          "Available verbs:\n" +
                          "- deb: Build a Debian/Ubuntu .deb installer.\n" +
                          "- rpm: Build an RPM (.rpm) package for Fedora, openSUSE and similar distributions.\n" +
                          "- appimage: Build a portable AppImage (.AppImage) bundle or work with AppDir workflows.\n\n" +
                          "Tip: run `dotnetpackaging <verb> --help` to see format-specific options."
        };

        // Global --verbose option (purely for discoverability; value already read above)
        var verboseOption = new Option<bool>("--verbose", "-v", "--debug", "-d")
        {
            Description = "Enable verbose logging",
            Recursive = true
        };
        rootCommand.Add(verboseOption);

        var debCommand = CreateCommand(
            "deb",
            "Debian package",
            ".deb",
            CreateDeb,
            "Create a Debian (.deb) installer for Debian and Ubuntu based distributions.",
            "pack-deb",
            "debian");
        AddDebFromProjectSubcommand(debCommand);
        rootCommand.Add(debCommand);

        var rpmCommand = CreateCommand(
            "rpm",
            "RPM package",
            ".rpm",
            CreateRpm,
            "Create an RPM (.rpm) package suitable for Fedora, openSUSE, and other RPM-based distributions.",
            "pack-rpm");
        // Add rpm from-project subcommand
        AddRpmFromProjectSubcommand(rpmCommand);
        rootCommand.Add(rpmCommand);

        var appImageCommand = CreateCommand(
            "appimage",
            "AppImage package",
            ".AppImage",
            CreateAppImage,
            "Create a portable AppImage (.AppImage) from a published directory. Use subcommands for AppDir workflows.",
            "pack-appimage");
        // Add subcommands for AppImage
        AddAppImageSubcommands(appImageCommand);
        AddAppImageFromProjectSubcommand(appImageCommand);
        rootCommand.Add(appImageCommand);

        // DMG command (experimental, cross-platform)
        var dmgCommand = CreateCommand(
            "dmg",
            "macOS disk image",
            ".dmg",
            CreateDmg,
            "Create a simple macOS disk image (.dmg). Currently uses an ISO/UDF (UDTO) payload for broad compatibility.",
            "pack-dmg");
        AddDmgFromProjectSubcommand(dmgCommand);
        rootCommand.Add(dmgCommand);

        // dmg verify subcommand
        var verifyFileOption = new Option<FileInfo>("--file")
        {
            Description = "Path to the .dmg file",
            Required = true
        };
        var verifyCmd = new Command("verify", "Verify that a .dmg file has a macOS-friendly structure (ISO/UDTO or UDIF).")
        {
            verifyFileOption
        };
        verifyCmd.SetAction(async parseResult =>
        {
            var file = parseResult.GetValue(verifyFileOption)!;
            await ExecuteWithLogging("dmg-verify", file.FullName, async logger =>
            {
                var result = await DotnetPackaging.Dmg.DmgVerifier.Verify(file.FullName);
                if (result.IsFailure)
                {
                    logger.Error("Verification failed: {Error}", result.Error);
                    Console.Error.WriteLine(result.Error);
                    Environment.ExitCode = 1;
                }
                else
                {
                    logger.Information("{VerificationResult}", result.Value);
                }
            });
        });
        dmgCommand.Add(verifyCmd);

        // Flatpak command
        var flatpakCommand = new Command("flatpak") { Description = "Flatpak packaging: generate layout, OSTree repo, or bundle (.flatpak). Can use system flatpak or internal bundler." };
        AddFlatpakSubcommands(flatpakCommand);
        AddFlatpakFromProjectSubcommand(flatpakCommand);
        rootCommand.Add(flatpakCommand);

        // MSIX command (experimental)
        var msixCommand = new Command("msix") { Description = "MSIX packaging (experimental)" };
        AddMsixSubcommands(msixCommand);
        rootCommand.Add(msixCommand);

        // EXE SFX command
        var exeCommand = new Command("exe") { Description = "Windows self-extracting installer (.exe). If --stub is not provided, the tool downloads the appropriate stub from GitHub Releases." };
        var exeInputDir = new Option<DirectoryInfo>("--directory")
        {
            Description = "The input directory (publish output)",
            Required = true
        };
        var exeOutput = new Option<FileInfo>("--output")
        {
            Description = "Output installer .exe",
            Required = true
        };
        var stubPath = new Option<FileInfo>("--stub")
        {
            Description = "Path to the prebuilt stub (WinExe) to concatenate (optional if repo layout is present)"
        };
        var exRidTop = new Option<string?>("--rid")
        {
            Description = "Runtime identifier for the stub (win-x64, win-arm64)"
        };

        // Reuse metadata options
        var exAppName = new Option<string>("--application-name")
        {
            Description = "Application name",
            Required = false
        };
        var exComment = new Option<string>("--comment")
        {
            Description = "Comment / long description",
            Required = false
        };
        var exVersion = new Option<string>("--version")
        {
            Description = "Version",
            Required = false
        };
        var exAppId = new Option<string>("--appId")
        {
            Description = "Application Id (Reverse DNS typical)",
            Required = false
        };
        var exVendor = new Option<string>("--vendor")
        {
            Description = "Vendor/Publisher",
            Required = false
        };
        var exExecutableName = new Option<string>("--executable-name")
        {
            Description = "Name of your application's executable",
            Required = false
        };
        var exIconOption = new Option<IIcon?>("--icon")
        {
            Description = "Path to the application icon"
        };
        exIconOption.CustomParser = GetIcon;

        var optionsBinder = new OptionsBinder(
            exAppName,
            new Option<string>("--wm-class"),
            new Option<IEnumerable<string>>("--keywords"),
            exComment,
            new Option<MainCategory?>("--main-category"),
            new Option<IEnumerable<AdditionalCategory>>("--additional-categories"),
            exIconOption,
            exVersion,
            new Option<Uri>("--homepage"),
            new Option<string>("--license"),
            new Option<IEnumerable<Uri>>("--screenshot-urls"),
            new Option<string>("--summary"),
            exAppId,
            exExecutableName,
            new Option<bool>("--is-terminal")
        );

        exeCommand.Add(exeInputDir);
        exeCommand.Add(exeOutput);
        exeCommand.Add(stubPath);
        // Make metadata options global so subcommands can use them without re-adding
        exAppName.Recursive = true;
        exComment.Recursive = true;
        exVersion.Recursive = true;
        exAppId.Recursive = true;
        exVendor.Recursive = true;
        exExecutableName.Recursive = true;
        exeCommand.Add(exAppName);
        exeCommand.Add(exComment);
        exeCommand.Add(exVersion);
        exeCommand.Add(exAppId);
        exeCommand.Add(exVendor);
        exeCommand.Add(exExecutableName);
        exeCommand.Add(exRidTop);

        exeCommand.SetAction(async parseResult =>
        {
            var inDir = parseResult.GetValue(exeInputDir)!;
            var outFile = parseResult.GetValue(exeOutput)!;
            var stub = parseResult.GetValue(stubPath);
            var opt = optionsBinder.Bind(parseResult);
            var vendorOpt = parseResult.GetValue(exVendor);
            var ridOpt = parseResult.GetValue(exRidTop);
            await ExecuteWithLogging("exe", outFile.FullName, async logger =>
            {
                var exeService = new ExePackagingService(logger);
                var result = await exeService.BuildFromDirectory(inDir, outFile, opt, vendorOpt, ridOpt, stub);
                if (result.IsFailure)
                {
                    logger.Error("EXE packaging failed: {Error}", result.Error);
                    Console.Error.WriteLine(result.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                logger.Information("{OutputFile}", result.Value.FullName);
            });
        });

        // exe from-project
        var exProject = new Option<FileInfo>("--project")
        {
            Description = "Path to the .csproj file",
            Required = true
        };
        var exRid = new Option<string?>("--rid")
        {
            Description = "Runtime identifier (e.g. win-x64, win-arm64)"
        };
        var exSelfContained = new Option<bool>("--self-contained")
        {
            Description = "Publish self-contained"
        };
        exSelfContained.DefaultValueFactory = _ => true;
        var exConfiguration = new Option<string>("--configuration")
        {
            Description = "Build configuration"
        };
        exConfiguration.DefaultValueFactory = _ => "Release";
        var exSingleFile = new Option<bool>("--single-file")
        {
            Description = "Publish single-file"
        };
        var exTrimmed = new Option<bool>("--trimmed")
        {
            Description = "Enable trimming"
        };
        var exOut = new Option<FileInfo>("--output")
        {
            Description = "Output installer .exe",
            Required = true
        };
        var exStub = new Option<FileInfo>("--stub")
        {
            Description = "Path to the prebuilt stub (WinExe) to concatenate (optional if repo layout is present)"
        };

        var exFromProject = new Command("from-project") { Description = "Publish a .NET project and build a Windows self-extracting installer (.exe). If --stub is not provided, the tool downloads the appropriate stub from GitHub Releases." };
        exFromProject.Add(exProject);
        exFromProject.Add(exRid);
        exFromProject.Add(exSelfContained);
        exFromProject.Add(exConfiguration);
        exFromProject.Add(exSingleFile);
        exFromProject.Add(exTrimmed);
        exFromProject.Add(exOut);
        exFromProject.Add(exStub);

        exFromProject.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(exProject)!;
            var ridVal = parseResult.GetValue(exRid);
            var sc = parseResult.GetValue(exSelfContained);
            var cfg = parseResult.GetValue(exConfiguration)!;
            var sf = parseResult.GetValue(exSingleFile);
            var tr = parseResult.GetValue(exTrimmed);
            var extrasOutput = parseResult.GetValue(exOut)!;
            var extrasStub = parseResult.GetValue(exStub);
            var vendorOpt = parseResult.GetValue(exVendor);
            var opt = optionsBinder.Bind(parseResult);
            await ExecuteWithLogging("exe-from-project", extrasOutput.FullName, async logger =>
            {
                var exeService = new ExePackagingService(logger);
                var result = await exeService.BuildFromProject(prj, ridVal, sc, cfg, sf, tr, extrasOutput, opt, vendorOpt, extrasStub);
                if (result.IsFailure)
                {
                    logger.Error("EXE from project packaging failed: {Error}", result.Error);
                    Console.Error.WriteLine(result.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                logger.Information("{OutputFile}", result.Value.FullName);
            });
        });

        exeCommand.Add(exFromProject);

        rootCommand.Add(exeCommand);
        
        var parseResult = rootCommand.Parse(args, configuration: null);
        var exitCode = await parseResult.InvokeAsync(parseResult.InvocationConfiguration, CancellationToken.None);
        Log.Information("dotnetpackaging completed with exit code {ExitCode}", exitCode);
        return exitCode;
    }

    private static bool IsVerboseRequested(string[] args)
    {
        if (EnvironmentVariableEnabled(VerboseEnvVar) || EnvironmentVariableEnabled(LegacyVerboseEnvVar))
        {
            return true;
        }

        return args.Any(IsVerboseToken);
    }

    private static bool EnvironmentVariableEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
               || value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVerboseToken(string token)
    {
        return string.Equals(token, "--verbose", StringComparison.OrdinalIgnoreCase)
               || string.Equals(token, "-v", StringComparison.OrdinalIgnoreCase)
               || string.Equals(token, "--debug", StringComparison.OrdinalIgnoreCase)
               || string.Equals(token, "-d", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetVerboseEnvironment(bool verbose)
    {
        Environment.SetEnvironmentVariable(VerboseEnvVar, verbose ? "1" : "0");
    }

    private static Command CreateCommand(
        string commandName,
        string friendlyName,
        string extension,
        Func<DirectoryInfo, FileInfo, Options, ILogger, Task> handler,
        string? description = null,
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
        iconOption.CustomParser = GetIcon;

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
        
        fromBuildDir.SetAction(async parseResult =>
        {
            var directory = parseResult.GetValue(buildDir)!;
            var output = parseResult.GetValue(outputFileOption)!;
            var opts = options.Bind(parseResult);
            await ExecuteWithLogging(commandName, output.FullName, logger => handler(directory, output, opts, logger));
        });
        return fromBuildDir;
    }

    private static async Task ExecuteWithLogging(string commandName, string target, Func<ILogger, Task> action)
    {
        using var scope = LogContext.PushProperty("Command", commandName);
        var stopwatch = Stopwatch.StartNew();
        Log.Information("{Command} started for {Target}", commandName, target);
        var logger = Log.ForContext("Command", commandName).ForContext("Target", target);
        try
        {
            await action(logger);
            Log.Information("{Command} completed for {Target} in {Elapsed}", commandName, target, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{Command} failed for {Target}", commandName, target);
            throw;
        }
    }

    private static Task CreateAppImage(DirectoryInfo inputDir, FileInfo outputFile, Options options, ILogger logger)
    {
        logger.Debug("Packaging AppImage from {Directory}", inputDir.FullName);
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
        var appName = new Option<string>("--application-name") { Description = "Application name", Required = false };
        var startupWmClass = new Option<string>("--wm-class") { Description = "Startup WM Class", Required = false };
        var mainCategory = new Option<MainCategory?>("--main-category") { Description = "Main category", Required = false, Arity = ArgumentArity.ZeroOrOne };
        var additionalCategories = new Option<IEnumerable<AdditionalCategory>>("--additional-categories") { Description = "Additional categories", Required = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var keywords = new Option<IEnumerable<string>>("--keywords") { Description = "Keywords", Required = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var comment = new Option<string>("--comment") { Description = "Comment", Required = false };
        var version = new Option<string>("--version") { Description = "Version", Required = false };
        var homePage = new Option<Uri>("--homepage") { Description = "Home page of the application", Required = false };
        var license = new Option<string>("--license") { Description = "License of the application", Required = false };
        var screenshotUrls = new Option<IEnumerable<Uri>>("--screenshot-urls") { Description = "Screenshot URLs", Required = false };
        var summary = new Option<string>("--summary") { Description = "Summary. Short description that should not end in a dot.", Required = false };
        var appId = new Option<string>("--appId") { Description = "Application Id. Usually a Reverse DNS name like com.SomeCompany.SomeApplication", Required = false };
        var executableName = new Option<string>("--executable-name") { Description = "Name of your application's executable", Required = false };
        var isTerminal = new Option<bool>("--is-terminal") { Description = "Indicates whether your application is a terminal application", Required = false };
        var iconOption = new Option<IIcon?>("--icon")
        {
            Required = false,
            Description = "Path to the application icon"
        };
        iconOption.CustomParser = GetIcon;

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
        var inputDir = new Option<DirectoryInfo>("--directory") { Description = "The input directory (publish output)", Required = true };
        var outputDir = new Option<DirectoryInfo>("--output-dir") { Description = "Destination directory for the AppDir", Required = true };
        var appDirCmd = new Command("appdir") { Description = "Creates an AppDir from a directory (does not package an .AppImage). For .NET apps, pass the publish directory." };
        appDirCmd.Add(inputDir);
        appDirCmd.Add(outputDir);
        appDirCmd.Add(appName);
        appDirCmd.Add(startupWmClass);
        appDirCmd.Add(mainCategory);
        appDirCmd.Add(additionalCategories);
        appDirCmd.Add(keywords);
        appDirCmd.Add(comment);
        appDirCmd.Add(version);
        appDirCmd.Add(homePage);
        appDirCmd.Add(license);
        appDirCmd.Add(screenshotUrls);
        appDirCmd.Add(summary);
        appDirCmd.Add(appId);
        appDirCmd.Add(executableName);
        appDirCmd.Add(isTerminal);
        appDirCmd.Add(iconOption);
        appDirCmd.SetAction(async parseResult =>
        {
            var directory = parseResult.GetValue(inputDir)!;
            var output = parseResult.GetValue(outputDir)!;
            var metadata = binder.Bind(parseResult);
            await ExecuteWithLogging("appimage-appdir", output.FullName, logger => CreateAppDir(directory, output, metadata, logger));
        });

        // appimage from-appdir
        var appDirPath = new Option<DirectoryInfo>("--directory") { Description = "The AppDir directory to package", Required = true };
        var outputFile = new Option<FileInfo>("--output") { Description = "Output .AppImage file", Required = true };
        var execRel = new Option<string>("--executable-relative-path") { Description = "Executable inside the AppDir (relative), e.g., usr/bin/MyApp", Required = false };
        var fromAppDirCmd = new Command("from-appdir") { Description = "Creates an AppImage from an existing AppDir directory." };
        fromAppDirCmd.Add(appDirPath);
        fromAppDirCmd.Add(outputFile);
        fromAppDirCmd.Add(execRel);
        fromAppDirCmd.Add(appName);
        fromAppDirCmd.Add(startupWmClass);
        fromAppDirCmd.Add(mainCategory);
        fromAppDirCmd.Add(additionalCategories);
        fromAppDirCmd.Add(keywords);
        fromAppDirCmd.Add(comment);
        fromAppDirCmd.Add(version);
        fromAppDirCmd.Add(homePage);
        fromAppDirCmd.Add(license);
        fromAppDirCmd.Add(screenshotUrls);
        fromAppDirCmd.Add(summary);
        fromAppDirCmd.Add(appId);
        fromAppDirCmd.Add(executableName);
        fromAppDirCmd.Add(isTerminal);
        fromAppDirCmd.Add(iconOption);
        fromAppDirCmd.SetAction(async parseResult =>
        {
            var directory = parseResult.GetValue(appDirPath)!;
            var output = parseResult.GetValue(outputFile)!;
            var relativeExec = parseResult.GetValue(execRel);
            var metadata = binder.Bind(parseResult);
            await ExecuteWithLogging("appimage-from-appdir", output.FullName, logger => CreateAppImageFromAppDir(directory, output, relativeExec, metadata, logger));
        });

        appImageCommand.Add(appDirCmd);
        appImageCommand.Add(fromAppDirCmd);
    }

    private static void AddFlatpakSubcommands(Command flatpakCommand)
    {
        // Options reused for metadata
        var appName = new Option<string>("--application-name") { Description = "Application name", Required = false };
        var startupWmClass = new Option<string>("--wm-class") { Description = "Startup WM Class", Required = false };
        var mainCategory = new Option<MainCategory?>("--main-category") { Description = "Main category", Required = false, Arity = ArgumentArity.ZeroOrOne };
        var additionalCategories = new Option<IEnumerable<AdditionalCategory>>("--additional-categories") { Description = "Additional categories", Required = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var keywords = new Option<IEnumerable<string>>("--keywords") { Description = "Keywords", Required = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var comment = new Option<string>("--comment") { Description = "Comment", Required = false };
        var version = new Option<string>("--version") { Description = "Version", Required = false };
        var homePage = new Option<Uri>("--homepage") { Description = "Home page of the application", Required = false };
        var license = new Option<string>("--license") { Description = "License of the application", Required = false };
        var screenshotUrls = new Option<IEnumerable<Uri>>("--screenshot-urls") { Description = "Screenshot URLs", Required = false };
        var summary = new Option<string>("--summary") { Description = "Summary. Short description that should not end in a dot.", Required = false };
        var appId = new Option<string>("--appId") { Description = "Application Id. Usually a Reverse DNS name like com.SomeCompany.SomeApplication", Required = false };
        var executableName = new Option<string>("--executable-name") { Description = "Name of your application's executable", Required = false };
        var isTerminal = new Option<bool>("--is-terminal") { Description = "Indicates whether your application is a terminal application", Required = false };
        var iconOption = new Option<IIcon?>("--icon")
        {
            Required = false,
            Description = "Path to the application icon"
        };
        iconOption.CustomParser = GetIcon;

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

        // Flatpak-specific options
        var fpRuntime = new Option<string>("--runtime") { Description = "Flatpak runtime (e.g. org.freedesktop.Platform)" };
        fpRuntime.DefaultValueFactory = _ => "org.freedesktop.Platform";
        var fpSdk = new Option<string>("--sdk") { Description = "Flatpak SDK (e.g. org.freedesktop.Sdk)" };
        fpSdk.DefaultValueFactory = _ => "org.freedesktop.Sdk";
        var fpBranch = new Option<string>("--branch") { Description = "Flatpak branch (e.g. stable)" };
        fpBranch.DefaultValueFactory = _ => "stable";
        var fpRuntimeVersion = new Option<string>("--runtime-version") { Description = "Flatpak runtime version" };
        fpRuntimeVersion.DefaultValueFactory = _ => "23.08";
        var fpShared = new Option<IEnumerable<string>>("--shared") { Description = "Flatpak [Context] shared permissions", AllowMultipleArgumentsPerToken = true };
        fpShared.DefaultValueFactory = _ => new[] { "network", "ipc" };
        var fpSockets = new Option<IEnumerable<string>>("--sockets") { Description = "Flatpak [Context] sockets", AllowMultipleArgumentsPerToken = true };
        fpSockets.DefaultValueFactory = _ => new[] { "wayland", "x11", "pulseaudio" };
        var fpDevices = new Option<IEnumerable<string>>("--devices") { Description = "Flatpak [Context] devices", AllowMultipleArgumentsPerToken = true };
        fpDevices.DefaultValueFactory = _ => new[] { "dri" };
        var fpFilesystems = new Option<IEnumerable<string>>("--filesystems") { Description = "Flatpak [Context] filesystems", AllowMultipleArgumentsPerToken = true };
        fpFilesystems.DefaultValueFactory = _ => new[] { "home" };
        var fpArch = new Option<string?>("--arch") { Description = "Override architecture (x86_64, aarch64, i386, armhf)" };
        var fpCommandOverride = new Option<string?>("--command") { Description = "Override command name inside Flatpak (defaults to AppId)" };

        var inputDir = new Option<DirectoryInfo>("--directory") { Description = "The input directory (publish output)", Required = true };
        var outputDir = new Option<DirectoryInfo>("--output-dir") { Description = "Destination directory for the Flatpak layout", Required = true };
        var layoutCmd = new Command("layout") { Description = "Creates a Flatpak layout (metadata, files/) from a published directory." };
        layoutCmd.Add(inputDir);
        layoutCmd.Add(outputDir);
        layoutCmd.Add(appName);
        layoutCmd.Add(startupWmClass);
        layoutCmd.Add(mainCategory);
        layoutCmd.Add(additionalCategories);
        layoutCmd.Add(keywords);
        layoutCmd.Add(comment);
        layoutCmd.Add(version);
        layoutCmd.Add(homePage);
        layoutCmd.Add(license);
        layoutCmd.Add(screenshotUrls);
        layoutCmd.Add(summary);
        layoutCmd.Add(appId);
        layoutCmd.Add(executableName);
        layoutCmd.Add(isTerminal);
        layoutCmd.Add(iconOption);
        layoutCmd.Add(fpRuntime);
        layoutCmd.Add(fpSdk);
        layoutCmd.Add(fpBranch);
        layoutCmd.Add(fpRuntimeVersion);
        layoutCmd.Add(fpShared);
        layoutCmd.Add(fpSockets);
        layoutCmd.Add(fpDevices);
        layoutCmd.Add(fpFilesystems);
        layoutCmd.Add(fpArch);
        layoutCmd.Add(fpCommandOverride);
        var fpBinder = new FlatpakOptionsBinder(fpRuntime, fpSdk, fpBranch, fpRuntimeVersion, fpShared, fpSockets, fpDevices, fpFilesystems, fpArch, fpCommandOverride);
        layoutCmd.SetAction(async parseResult =>
        {
            var directory = parseResult.GetValue(inputDir)!;
            var output = parseResult.GetValue(outputDir)!;
            var opts = binder.Bind(parseResult);
            var fpOpts = fpBinder.Bind(parseResult);
            await ExecuteWithLogging("flatpak-layout", output.FullName, logger => CreateFlatpakLayout(directory, output, opts, fpOpts, logger));
        });

        flatpakCommand.Add(layoutCmd);

        var bundleInputDir = new Option<DirectoryInfo>("--directory") { Description = "The input directory (publish output)", Required = true };
        var bundleOutput = new Option<FileInfo>("--output") { Description = "Output .flatpak file", Required = true };
        var bundleCmd = new Command("bundle") { Description = "Creates a single-file .flatpak bundle from a published directory. By default uses internal bundler; pass --system to use installed flatpak." };
        bundleCmd.Add(bundleInputDir);
        bundleCmd.Add(bundleOutput);
        var useSystem = new Option<bool>("--system") { Description = "Use system 'flatpak' (build-export/build-bundle) if available" };
        bundleCmd.Add(useSystem);
        bundleCmd.Add(appName);
        bundleCmd.Add(startupWmClass);
        bundleCmd.Add(mainCategory);
        bundleCmd.Add(additionalCategories);
        bundleCmd.Add(keywords);
        bundleCmd.Add(comment);
        bundleCmd.Add(version);
        bundleCmd.Add(homePage);
        bundleCmd.Add(license);
        bundleCmd.Add(screenshotUrls);
        bundleCmd.Add(summary);
        bundleCmd.Add(appId);
        bundleCmd.Add(executableName);
        bundleCmd.Add(isTerminal);
        bundleCmd.Add(iconOption);
        bundleCmd.Add(fpRuntime);
        bundleCmd.Add(fpSdk);
        bundleCmd.Add(fpBranch);
        bundleCmd.Add(fpRuntimeVersion);
        bundleCmd.Add(fpShared);
        bundleCmd.Add(fpSockets);
        bundleCmd.Add(fpDevices);
        bundleCmd.Add(fpFilesystems);
        bundleCmd.Add(fpArch);
        bundleCmd.Add(fpCommandOverride);
        bundleCmd.SetAction(async parseResult =>
        {
            var directory = parseResult.GetValue(bundleInputDir)!;
            var output = parseResult.GetValue(bundleOutput)!;
            var system = parseResult.GetValue(useSystem);
            var opts = binder.Bind(parseResult);
            var fpOpts = fpBinder.Bind(parseResult);
            await ExecuteWithLogging("flatpak-bundle", output.FullName, logger => CreateFlatpakBundle(directory, output, system, opts, fpOpts, logger));
        });

        flatpakCommand.Add(bundleCmd);

        // flatpak from-project (bundle)
        var fpPrj = new Option<FileInfo>("--project") { Description = "Path to the .csproj file", Required = true };
        var fpOut = new Option<FileInfo>("--output") { Description = "Output .flatpak file", Required = true };
        var fpUseSystem = new Option<bool>("--system") { Description = "Use system 'flatpak' (build-export/build-bundle) if available" };
        var fromProjectCmd = new Command("from-project") { Description = "Publish a .NET project and build a .flatpak bundle." };
        fromProjectCmd.Add(fpPrj);
        fromProjectCmd.Add(fpOut);
        fromProjectCmd.Add(fpUseSystem);
        fromProjectCmd.Add(appName);
        fromProjectCmd.Add(startupWmClass);
        fromProjectCmd.Add(mainCategory);
        fromProjectCmd.Add(additionalCategories);
        fromProjectCmd.Add(keywords);
        fromProjectCmd.Add(comment);
        fromProjectCmd.Add(version);
        fromProjectCmd.Add(homePage);
        fromProjectCmd.Add(license);
        fromProjectCmd.Add(screenshotUrls);
        fromProjectCmd.Add(summary);
        fromProjectCmd.Add(appId);
        fromProjectCmd.Add(executableName);
        fromProjectCmd.Add(isTerminal);
        fromProjectCmd.Add(iconOption);
        fromProjectCmd.Add(fpRuntime);
        fromProjectCmd.Add(fpSdk);
        fromProjectCmd.Add(fpBranch);
        fromProjectCmd.Add(fpRuntimeVersion);
        fromProjectCmd.Add(fpShared);
        fromProjectCmd.Add(fpSockets);
        fromProjectCmd.Add(fpDevices);
        fromProjectCmd.Add(fpFilesystems);
        fromProjectCmd.Add(fpArch);
        fromProjectCmd.Add(fpCommandOverride);
        fromProjectCmd.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(fpPrj)!;
            var outFile = parseResult.GetValue(fpOut)!;
            var useSystemBundler = parseResult.GetValue(fpUseSystem);
            var opt = binder.Bind(parseResult);
            var fopt = fpBinder.Bind(parseResult);

            await ExecuteWithLogging("flatpak-from-project", outFile.FullName, async logger =>
            {
                var publisher = new DotnetPackaging.Publish.DotnetPublisher(Maybe<ILogger>.From(logger));
                var req = new DotnetPackaging.Publish.ProjectPublishRequest(prj.FullName)
                {
                    SelfContained = false,
                    Configuration = "Release",
                    SingleFile = false,
                    Trimmed = false
                };

                var pub = await publisher.Publish(req);
                if (pub.IsFailure)
                {
                    logger.Error("Failed publishing project {Project}: {Error}", prj.FullName, pub.Error);
                    Console.Error.WriteLine(pub.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var root = pub.Value.Container;
                var setup = new FromDirectoryOptions();
                setup.From(opt);

                var execRes = await BuildUtils.GetExecutable(root, setup, logger);
                if (execRes.IsFailure)
                {
                    logger.Error("Executable discovery failed: {Error}", execRes.Error);
                    Console.Error.WriteLine(execRes.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var archRes = await BuildUtils.GetArch(setup, execRes.Value);
                if (archRes.IsFailure)
                {
                    logger.Error("Architecture detection failed: {Error}", archRes.Error);
                    Console.Error.WriteLine(archRes.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var pm = await BuildUtils.CreateMetadata(setup, root, archRes.Value, execRes.Value, opt.IsTerminal.GetValueOrDefault(false), pub.Value.Name, logger);
                var planRes = await new FlatpakFactory().BuildPlan(root, pm, fopt);
                if (planRes.IsFailure)
                {
                    logger.Error("Flatpak plan generation failed: {Error}", planRes.Error);
                    Console.Error.WriteLine(planRes.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var plan = planRes.Value;

                if (!useSystemBundler)
                {
                    var internalBytes = FlatpakBundle.CreateOstree(plan);
                    if (internalBytes.IsFailure)
                    {
                        logger.Error("Flatpak bundle creation failed: {Error}", internalBytes.Error);
                        Console.Error.WriteLine(internalBytes.Error);
                        Environment.ExitCode = 1;
                        return;
                    }

                    var wr = await internalBytes.Value.WriteTo(outFile.FullName);
                    if (wr.IsFailure)
                    {
                        logger.Error("Failed writing bundle: {Error}", wr.Error);
                        Console.Error.WriteLine(wr.Error);
                        Environment.ExitCode = 1;
                        return;
                    }

                    logger.Information("{OutputFile}", outFile.FullName);
                    return;
                }

                var tmpAppDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-flatpak-app-" + Guid.NewGuid());
                var tmpRepoDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-flatpak-repo-" + Guid.NewGuid());
                Directory.CreateDirectory(tmpAppDir);
                Directory.CreateDirectory(tmpRepoDir);

                var write = await plan.ToRootContainer().WriteTo(tmpAppDir);
                if (write.IsFailure)
                {
                    logger.Error("Failed materializing Flatpak plan: {Error}", write.Error);
                    Console.Error.WriteLine(write.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                MakeExecutable(System.IO.Path.Combine(tmpAppDir, "files", "bin", plan.CommandName));
                MakeExecutable(System.IO.Path.Combine(tmpAppDir, "files", plan.ExecutableTargetPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));

                var effArch = fopt.ArchitectureOverride.GetValueOrDefault(plan.Metadata.Architecture).PackagePrefix;
                var cmd = fopt.CommandOverride.GetValueOrDefault(plan.CommandName);
                var finishArgs = $"build-finish \"{tmpAppDir}\" --command={cmd} {string.Join(" ", fopt.Shared.Select(s => $"--share={s}"))} {string.Join(" ", fopt.Sockets.Select(s => $"--socket={s}"))} {string.Join(" ", fopt.Devices.Select(d => $"--device={d}"))} {string.Join(" ", fopt.Filesystems.Select(f => $"--filesystem={f}"))}";
                var finish = Run("flatpak", finishArgs);
                if (finish.IsFailure)
                {
                    logger.Error("flatpak build-finish failed: {Error}", finish.Error);
                    Console.Error.WriteLine(finish.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var export = Run("flatpak", $"build-export --arch={effArch} \"{tmpRepoDir}\" \"{tmpAppDir}\" {fopt.Branch}");
                if (export.IsFailure)
                {
                    logger.Error("flatpak build-export failed: {Error}", export.Error);
                    Console.Error.WriteLine(export.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var bundle = Run("flatpak", $"build-bundle \"{tmpRepoDir}\" \"{outFile.FullName}\" {plan.AppId} {fopt.Branch} --arch={effArch}");
                if (bundle.IsFailure)
                {
                    logger.Error("flatpak build-bundle failed: {Error}", bundle.Error);
                    Console.Error.WriteLine(bundle.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                logger.Information("{OutputFile}", outFile.FullName);
            });
        });

        flatpakCommand.Add(fromProjectCmd);

        var repoInputDir = new Option<DirectoryInfo>("--directory") { Description = "The input directory (publish output)", Required = true };
        var repoOutDir = new Option<DirectoryInfo>("--output-dir") { Description = "Destination directory for the OSTree repo", Required = true };
        var repoCmd = new Command("repo") { Description = "Creates an OSTree repo directory from a published directory (debug/validation)." };
        repoCmd.Add(repoInputDir);
        repoCmd.Add(repoOutDir);
        repoCmd.Add(appName);
        repoCmd.Add(startupWmClass);
        repoCmd.Add(mainCategory);
        repoCmd.Add(additionalCategories);
        repoCmd.Add(keywords);
        repoCmd.Add(comment);
        repoCmd.Add(version);
        repoCmd.Add(homePage);
        repoCmd.Add(license);
        repoCmd.Add(screenshotUrls);
        repoCmd.Add(summary);
        repoCmd.Add(appId);
        repoCmd.Add(executableName);
        repoCmd.Add(isTerminal);
        repoCmd.Add(iconOption);
        repoCmd.Add(fpRuntime);
        repoCmd.Add(fpSdk);
        repoCmd.Add(fpBranch);
        repoCmd.Add(fpRuntimeVersion);
        repoCmd.Add(fpShared);
        repoCmd.Add(fpSockets);
        repoCmd.Add(fpDevices);
        repoCmd.Add(fpFilesystems);
        repoCmd.Add(fpArch);
        repoCmd.Add(fpCommandOverride);
        repoCmd.SetAction(async parseResult =>
        {
            var directory = parseResult.GetValue(repoInputDir)!;
            var output = parseResult.GetValue(repoOutDir)!;
            var opts = binder.Bind(parseResult);
            var fpOpts = fpBinder.Bind(parseResult);
            await ExecuteWithLogging("flatpak-repo", output.FullName, logger => CreateFlatpakRepo(directory, output, opts, fpOpts, logger));
        });

        flatpakCommand.Add(repoCmd);

        // flatpak pack (minimal UX): only directory and output-dir, prefer system bundler with fallback
        var packInputDir = new Option<DirectoryInfo>("--directory") { Description = "The input directory (publish output)", Required = true };
        var packOutputDir = new Option<DirectoryInfo>("--output-dir") { Description = "Destination directory for the resulting .flatpak", Required = true };
        var packCmd = new Command("pack") { Description = "Pack a .flatpak with minimal parameters. Uses system flatpak if available, otherwise falls back to internal bundler." };
        packCmd.Add(packInputDir);
        packCmd.Add(packOutputDir);
        packCmd.SetAction(async parseResult =>
        {
            var inDir = parseResult.GetValue(packInputDir)!;
            var outDir = parseResult.GetValue(packOutputDir)!;
            await ExecuteWithLogging("flatpak-pack", outDir.FullName, async logger =>
            {
                var dirInfo = FileSystem.DirectoryInfo.New(inDir.FullName);
                var container = new DirectoryContainer(dirInfo);
                var root = container.AsRoot();
                var setup = new FromDirectoryOptions();

                var execRes = await BuildUtils.GetExecutable(root, setup, logger);
                if (execRes.IsFailure) { logger.Error("Executable discovery failed: {Error}", execRes.Error); Console.Error.WriteLine(execRes.Error); return; }
                var archRes = await BuildUtils.GetArch(setup, execRes.Value);
                if (archRes.IsFailure) { logger.Error("Architecture detection failed: {Error}", archRes.Error); Console.Error.WriteLine(archRes.Error); return; }
                var pm = await BuildUtils.CreateMetadata(setup, root, archRes.Value, execRes.Value, setup.IsTerminal, Maybe<string>.From(inDir.Name), logger);
                var planRes = await new FlatpakFactory().BuildPlan(root, pm, new FlatpakOptions());
                if (planRes.IsFailure) { logger.Error("Flatpak plan generation failed: {Error}", planRes.Error); Console.Error.WriteLine(planRes.Error); return; }
                var plan = planRes.Value;

                var fileName = $"{plan.AppId}_{plan.Metadata.Version}_{plan.Metadata.Architecture.PackagePrefix}.flatpak";
                var outPath = System.IO.Path.Combine(outDir.FullName, fileName);

                // Try system bundler first
                var tmpAppDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-flatpak-app-" + Guid.NewGuid());
                var tmpRepoDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-flatpak-repo-" + Guid.NewGuid());
                Directory.CreateDirectory(tmpAppDir);
                Directory.CreateDirectory(tmpRepoDir);
                var write = await plan.ToRootContainer().WriteTo(tmpAppDir);
                if (write.IsSuccess)
                {
                    MakeExecutable(System.IO.Path.Combine(tmpAppDir, "files", "bin", plan.CommandName));
                    MakeExecutable(System.IO.Path.Combine(tmpAppDir, "files", plan.ExecutableTargetPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
                    var arch = plan.Metadata.Architecture.PackagePrefix;
                    var finish = Run("flatpak", $"build-finish \"{tmpAppDir}\" --command={plan.CommandName} --socket=wayland --socket=x11 --socket=pulseaudio --share=network --share=ipc --device=dri --filesystem=home");
                    if (finish.IsSuccess)
                    {
                        var export = Run("flatpak", $"build-export --arch={arch} \"{tmpRepoDir}\" \"{tmpAppDir}\" stable");
                        if (export.IsSuccess)
                        {
                            var bundle = Run("flatpak", $"build-bundle \"{tmpRepoDir}\" \"{outPath}\" {plan.AppId} stable --arch={arch}");
                            if (bundle.IsSuccess)
                            {
                                logger.Information("{OutputFile}", outPath);
                                return;
                            }
                            else
                            {
                                logger.Debug("System flatpak build-bundle failed: {Error}", bundle.Error);
                            }
                        }
                        else
                        {
                            logger.Debug("System flatpak build-export failed: {Error}", export.Error);
                        }
                    }
                    else
                    {
                        logger.Debug("System flatpak build-finish failed: {Error}", finish.Error);
                    }
                }
                else
                {
                    logger.Debug("Failed to materialize plan in temp directory: {Error}", write.Error);
                }

                // Fallback: internal bundler
                var internalBytes = FlatpakBundle.CreateOstree(plan);
                if (internalBytes.IsFailure)
                {
                    logger.Error("Internal bundler failed: {Error}", internalBytes.Error);
                    Console.Error.WriteLine(internalBytes.Error);
                    return;
                }
                var writeRes = await internalBytes.Value.WriteTo(outPath);
                if (writeRes.IsFailure)
                {
                    logger.Error("Failed writing bundle: {Error}", writeRes.Error);
                    Console.Error.WriteLine(writeRes.Error);
                }
                else
                {
                    logger.Information("{OutputFile}", outPath);
                }
            });
        });
        
        flatpakCommand.Add(packCmd);
    }

    private static Task CreateAppDir(DirectoryInfo inputDir, DirectoryInfo outputDir, Options options, ILogger logger)
    {
        logger.Debug("Building AppDir from {Directory}", inputDir.FullName);
        var dirInfo = FileSystem.DirectoryInfo.New(inputDir.FullName);
        var container = new DirectoryContainer(dirInfo);
        var root = container.AsRoot();

        var metadata = BuildAppImageMetadata(options, inputDir);
        var factory = new AppImageFactory();

        return factory.BuildAppDir(root, metadata)
            .Bind(rootDir => rootDir.WriteTo(outputDir.FullName))
            .WriteResult();
    }

    private static Task CreateFlatpakLayout(DirectoryInfo inputDir, DirectoryInfo outputDir, Options options, FlatpakOptions flatpakOptions, ILogger logger)
    {
        logger.Debug("Generating Flatpak layout from {Directory}", inputDir.FullName);
        var dirInfo = FileSystem.DirectoryInfo.New(inputDir.FullName);
        var container = new DirectoryContainer(dirInfo);
        var root = container.AsRoot();

        // Build generic metadata from directory
        var setup = new FromDirectoryOptions();
        setup.From(options);

        return BuildUtils.GetExecutable(root, setup, logger)
            .Bind(exec => BuildUtils.GetArch(setup, exec).Map(a => (exec, a)))
            .Bind(async tuple =>
            {
                var pm = await BuildUtils.CreateMetadata(setup, root, tuple.a, tuple.exec, options.IsTerminal.GetValueOrDefault(false), Maybe<string>.From(inputDir.Name), logger);
                return Result.Success(pm);
            })
            .Bind(packageMetadata => new FlatpakFactory().BuildPlan(root, packageMetadata, flatpakOptions))
            .Bind(plan => plan.ToRootContainer().WriteTo(outputDir.FullName))
            .WriteResult();
    }

    private static Task CreateFlatpakBundle(DirectoryInfo inputDir, FileInfo outputFile, bool useSystem, Options options, FlatpakOptions flatpakOptions, ILogger logger)
    {
        logger.Debug("Creating Flatpak bundle (useSystem={UseSystem}) from {Directory}", useSystem, inputDir.FullName);
        var dirInfo = FileSystem.DirectoryInfo.New(inputDir.FullName);
        var container = new DirectoryContainer(dirInfo);
        var root = container.AsRoot();

        var setup = new FromDirectoryOptions();
        setup.From(options);

        return BuildUtils.GetExecutable(root, setup, logger)
            .Bind(exec => BuildUtils.GetArch(setup, exec).Map(a => (exec, a)))
            .Bind(async tuple =>
            {
                var pm = await BuildUtils.CreateMetadata(setup, root, tuple.a, tuple.exec, options.IsTerminal.GetValueOrDefault(false), Maybe<string>.From(inputDir.Name), logger);
                return Result.Success(pm);
            })
            .Bind(packageMetadata => new FlatpakFactory().BuildPlan(root, packageMetadata, flatpakOptions))
            .Bind(plan =>
            {
                if (!useSystem)
                {
                    return FlatpakBundle.CreateOstree(plan)
                        .Bind(bytes => bytes.WriteTo(outputFile.FullName));
                }

                return Result.Success(plan).Bind(async p =>
                {
                    var tmpAppDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-flatpak-app-" + Guid.NewGuid());
                    var tmpRepoDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-flatpak-repo-" + Guid.NewGuid());
                    Directory.CreateDirectory(tmpAppDir);
                    Directory.CreateDirectory(tmpRepoDir);

                    var write = await p.ToRootContainer().WriteTo(tmpAppDir);
                    if (write.IsFailure) return Result.Failure(write.Error);

                    // Ensure wrapper and executable are marked executable in the AppDir
                    var wrapperPath = System.IO.Path.Combine(tmpAppDir, "files", "bin", p.CommandName);
                    var exePath = System.IO.Path.Combine(tmpAppDir, "files", p.ExecutableTargetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                    MakeExecutable(wrapperPath);
                    MakeExecutable(exePath);

                    var effArch = flatpakOptions.ArchitectureOverride.GetValueOrDefault(p.Metadata.Architecture).PackagePrefix;
                    var appId = p.AppId;
                    var cmd = flatpakOptions.CommandOverride.GetValueOrDefault(p.CommandName);
                    var finishArgs = $"build-finish \"{tmpAppDir}\" --command={cmd} {string.Join(" ", flatpakOptions.Shared.Select(s => $"--share={s}"))} {string.Join(" ", flatpakOptions.Sockets.Select(s => $"--socket={s}"))} {string.Join(" ", flatpakOptions.Devices.Select(d => $"--device={d}"))} {string.Join(" ", flatpakOptions.Filesystems.Select(f => $"--filesystem={f}"))}";
                    var finish = Run("flatpak", finishArgs);
                    if (finish.IsFailure) return Result.Failure(finish.Error);
                    var export = Run("flatpak", $"build-export --arch={effArch} \"{tmpRepoDir}\" \"{tmpAppDir}\" {flatpakOptions.Branch}");
                    if (export.IsFailure) return Result.Failure(export.Error);
                    var bundle = Run("flatpak", $"build-bundle \"{tmpRepoDir}\" \"{outputFile.FullName}\" {appId} {flatpakOptions.Branch} --arch={effArch}");
                    if (bundle.IsFailure) return Result.Failure(bundle.Error);
                    return Result.Success();
                });
            })
            .WriteResult();
    }

    private static Task CreateAppImageFromAppDir(DirectoryInfo appDir, FileInfo outputFile, string? executableRelativePath, Options options, ILogger logger)
    {
        logger.Debug("Packaging AppImage from AppDir {Directory}", appDir.FullName);
        var dirInfo = FileSystem.DirectoryInfo.New(appDir.FullName);
        var container = new DirectoryContainer(dirInfo);

        var metadata = BuildAppImageMetadata(options, appDir);
        var factory = new AppImageFactory();

        return factory.CreateFromAppDir(container, metadata, executableRelativePath, null)
            .Bind(x => x.ToByteSource())
            .Bind(source => source.WriteTo(outputFile.FullName))
            .WriteResult();
    }

    private static Task CreateFlatpakRepo(DirectoryInfo inputDir, DirectoryInfo outputDir, Options options, FlatpakOptions flatpakOptions, ILogger logger)
    {
        logger.Debug("Building Flatpak repo from {Directory}", inputDir.FullName);
        var dirInfo = FileSystem.DirectoryInfo.New(inputDir.FullName);
        var container = new DirectoryContainer(dirInfo);
        var root = container.AsRoot();

        var setup = new FromDirectoryOptions();
        setup.From(options);

        return BuildUtils.GetExecutable(root, setup, logger)
            .Bind(exec => BuildUtils.GetArch(setup, exec).Map(a => (exec, a)))
            .Bind(async tuple =>
            {
                var pm = await BuildUtils.CreateMetadata(setup, root, tuple.a, tuple.exec, options.IsTerminal.GetValueOrDefault(false), Maybe<string>.From(inputDir.Name), logger);
                return Result.Success(pm);
            })
            .Bind(packageMetadata => new FlatpakFactory().BuildPlan(root, packageMetadata, flatpakOptions))
            .Bind(plan => DotnetPackaging.Flatpak.Ostree.OstreeRepoBuilder.Build(plan))
            .Bind(repo => repo.WriteTo(outputDir.FullName))
            .WriteResult();
    }

    private static void MakeExecutable(string path)
    {
        try
        {
            var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                       UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                       UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
            if (System.IO.File.Exists(path))
            {
                System.IO.File.SetUnixFileMode(path, mode);
            }
        }
        catch
        {
            // ignore; best-effort
        }
    }

    private static Result Run(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)!;
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                var err = proc.StandardError.ReadToEnd();
                var stdout = proc.StandardOutput.ReadToEnd();
                return Result.Failure($"{fileName} {arguments}\nExitCode: {proc.ExitCode}\n{stdout}\n{err}");
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static Task CreateDeb(DirectoryInfo inputDir, FileInfo outputFile, Options options, ILogger logger)
    {
        logger.Debug("Packaging Debian artifact from {Directory}", inputDir.FullName);
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

    private static Task CreateRpm(DirectoryInfo inputDir, FileInfo outputFile, Options options, ILogger logger)
    {
        logger.Debug("Packaging RPM artifact from {Directory}", inputDir.FullName);
        return new Zafiro.FileSystem.Local.Directory(FileSystem.DirectoryInfo.New(inputDir.FullName))
            .ToDirectory()
            .Bind(directory => RpmFile.From()
                .Directory(directory)
                .Configure(configuration => configuration.From(options))
                .Build()
                .Bind(rpmFile => CopyRpmToOutput(rpmFile, outputFile)))
            .WriteResult();
    }

    private static void AddFlatpakFromProjectSubcommand(Command flatpakCommand)
    {
        // Implemented inside AddFlatpakSubcommands for access to shared options; placeholder to satisfy compiler
    }

    private static void AddRpmFromProjectSubcommand(Command rpmCommand)
    {
        var project = new Option<FileInfo>("--project") { Description = "Path to the .csproj file", Required = true };
        var rid = new Option<string?>("--rid") { Description = "Runtime identifier (e.g. linux-x64, linux-arm64)" };
        var selfContained = new Option<bool>("--self-contained") { Description = "Publish self-contained" };
        selfContained.DefaultValueFactory = _ => true;
        var configuration = new Option<string>("--configuration") { Description = "Build configuration" };
        configuration.DefaultValueFactory = _ => "Release";
        var singleFile = new Option<bool>("--single-file") { Description = "Publish single-file" };
        var trimmed = new Option<bool>("--trimmed") { Description = "Enable trimming" };
        var output = new Option<FileInfo>("--output") { Description = "Destination path for the generated .rpm", Required = true };

        // Reuse metadata options via OptionsBinder
        var appName = new Option<string>("--application-name") { Description = "Application name", Required = false };
        var startupWmClass = new Option<string>("--wm-class") { Description = "Startup WM Class", Required = false };
        var mainCategory = new Option<MainCategory?>("--main-category") { Description = "Main category", Required = false, Arity = ArgumentArity.ZeroOrOne, };
        var additionalCategories = new Option<IEnumerable<AdditionalCategory>>("--additional-categories") { Description = "Additional categories", Required = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var keywords = new Option<IEnumerable<string>>("--keywords") { Description = "Keywords", Required = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var comment = new Option<string>("--comment") { Description = "Comment", Required = false };
        var version = new Option<string>("--version") { Description = "Version", Required = false };
        var homePage = new Option<Uri>("--homepage") { Description = "Home page of the application", Required = false };
        var license = new Option<string>("--license") { Description = "License of the application", Required = false };
        var screenshotUrls = new Option<IEnumerable<Uri>>("--screenshot-urls") { Description = "Screenshot URLs", Required = false };
        var summary = new Option<string>("--summary") { Description = "Summary. Short description that should not end in a dot.", Required = false };
        var appId = new Option<string>("--appId") { Description = "Application Id. Usually a Reverse DNS name like com.SomeCompany.SomeApplication", Required = false };
        var executableName = new Option<string>("--executable-name") { Description = "Name of your application's executable", Required = false };
        var isTerminal = new Option<bool>("--is-terminal") { Description = "Indicates whether your application is a terminal application", Required = false };
        var iconOption = new Option<IIcon?>("--icon") { Required = false, Description = "Path to the application icon" };
        iconOption.CustomParser = GetIcon;

        var optionsBinder = new OptionsBinder(appName, startupWmClass, keywords, comment, mainCategory, additionalCategories, iconOption, version, homePage, license, screenshotUrls, summary, appId, executableName, isTerminal);

        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build an RPM from the published output (no code duplication; library drives the pipeline)." };
        fromProject.Add(project);
        fromProject.Add(rid);
        fromProject.Add(selfContained);
        fromProject.Add(configuration);
        fromProject.Add(singleFile);
        fromProject.Add(trimmed);
        fromProject.Add(output);
        fromProject.Add(appName);
        fromProject.Add(startupWmClass);
        fromProject.Add(mainCategory);
        fromProject.Add(additionalCategories);
        fromProject.Add(keywords);
        fromProject.Add(comment);
        fromProject.Add(version);
        fromProject.Add(homePage);
        fromProject.Add(license);
        fromProject.Add(screenshotUrls);
        fromProject.Add(summary);
        fromProject.Add(appId);
        fromProject.Add(executableName);
        fromProject.Add(isTerminal);
        fromProject.Add(iconOption);

        fromProject.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(project)!;
            var ridVal = parseResult.GetValue(rid);
            var sc = parseResult.GetValue(selfContained);
            var cfg = parseResult.GetValue(configuration)!;
            var sf = parseResult.GetValue(singleFile);
            var tr = parseResult.GetValue(trimmed);
            var outFile = parseResult.GetValue(output)!;
            var opt = optionsBinder.Bind(parseResult);

            if (string.IsNullOrWhiteSpace(ridVal) && !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.Error.WriteLine("--rid is required when building RPM from-project on non-Linux hosts (e.g., linux-x64/linux-arm64).");
                Environment.ExitCode = 1;
                return;
            }

            var publisher = new DotnetPackaging.Publish.DotnetPublisher();
            var req = new DotnetPackaging.Publish.ProjectPublishRequest(prj.FullName)
            {
                Rid = string.IsNullOrWhiteSpace(ridVal) ? Maybe<string>.None : Maybe<string>.From(ridVal!),
                SelfContained = sc,
                Configuration = cfg,
                SingleFile = sf,
                Trimmed = tr
            };

            var pub = await publisher.Publish(req);
            if (pub.IsFailure)
            {
                Console.Error.WriteLine(pub.Error);
                Environment.ExitCode = 1;
                return;
            }

            var container = pub.Value.Container;
            var name = pub.Value.Name.Match(value => value, () => (string?)null);
            var builder = RpmFile.From().Container(container, name);
            var built = await builder.Configure(o => o.From(opt)).Build();
            if (built.IsFailure)
            {
                Console.Error.WriteLine(built.Error);
                Environment.ExitCode = 1;
                return;
            }

            var copy = CopyRpmToOutput(built.Value, outFile);
            if (copy.IsFailure)
            {
                Console.Error.WriteLine(copy.Error);
                Environment.ExitCode = 1;
            }
        });

        rpmCommand.Add(fromProject);
    }

    private static void AddDebFromProjectSubcommand(Command debCommand)
    {
        var project = new Option<FileInfo>("--project") { Description = "Path to the .csproj file", Required = true };
        var rid = new Option<string?>("--rid") { Description = "Runtime identifier (e.g. linux-x64, linux-arm64)" };
        var selfContained = new Option<bool>("--self-contained") { Description = "Publish self-contained" };
        selfContained.DefaultValueFactory = _ => true;
        var configuration = new Option<string>("--configuration") { Description = "Build configuration" };
        configuration.DefaultValueFactory = _ => "Release";
        var singleFile = new Option<bool>("--single-file") { Description = "Publish single-file" };
        var trimmed = new Option<bool>("--trimmed") { Description = "Enable trimming" };
        var output = new Option<FileInfo>("--output") { Description = "Destination path for the generated .deb", Required = true };

        var appName = new Option<string>("--application-name") { Description = "Application name", Required = false };
        var startupWmClass = new Option<string>("--wm-class") { Description = "Startup WM Class", Required = false };
        var mainCategory = new Option<MainCategory?>("--main-category") { Description = "Main category", Required = false, Arity = ArgumentArity.ZeroOrOne, };
        var additionalCategories = new Option<IEnumerable<AdditionalCategory>>("--additional-categories") { Description = "Additional categories", Required = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var keywords = new Option<IEnumerable<string>>("--keywords") { Description = "Keywords", Required = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var comment = new Option<string>("--comment") { Description = "Comment", Required = false };
        var version = new Option<string>("--version") { Description = "Version", Required = false };
        var homePage = new Option<Uri>("--homepage") { Description = "Home page of the application", Required = false };
        var license = new Option<string>("--license") { Description = "License of the application", Required = false };
        var screenshotUrls = new Option<IEnumerable<Uri>>("--screenshot-urls") { Description = "Screenshot URLs", Required = false };
        var summary = new Option<string>("--summary") { Description = "Summary. Short description that should not end in a dot.", Required = false };
        var appId = new Option<string>("--appId") { Description = "Application Id. Usually a Reverse DNS name like com.SomeCompany.SomeApplication", Required = false };
        var executableName = new Option<string>("--executable-name") { Description = "Name of your application's executable", Required = false };
        var isTerminal = new Option<bool>("--is-terminal") { Description = "Indicates whether your application is a terminal application", Required = false };
        var iconOption = new Option<IIcon?>("--icon") { Required = false, Description = "Path to the application icon" };
        iconOption.CustomParser = GetIcon;

        var optionsBinder = new OptionsBinder(appName, startupWmClass, keywords, comment, mainCategory, additionalCategories, iconOption, version, homePage, license, screenshotUrls, summary, appId, executableName, isTerminal);

        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build a Debian .deb from the published output." };
        fromProject.Add(project);
        fromProject.Add(rid);
        fromProject.Add(selfContained);
        fromProject.Add(configuration);
        fromProject.Add(singleFile);
        fromProject.Add(trimmed);
        fromProject.Add(output);
        fromProject.Add(appName);
        fromProject.Add(startupWmClass);
        fromProject.Add(mainCategory);
        fromProject.Add(additionalCategories);
        fromProject.Add(keywords);
        fromProject.Add(comment);
        fromProject.Add(version);
        fromProject.Add(homePage);
        fromProject.Add(license);
        fromProject.Add(screenshotUrls);
        fromProject.Add(summary);
        fromProject.Add(appId);
        fromProject.Add(executableName);
        fromProject.Add(isTerminal);
        fromProject.Add(iconOption);

        fromProject.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(project)!;
            var ridVal = parseResult.GetValue(rid);
            var sc = parseResult.GetValue(selfContained);
            var cfg = parseResult.GetValue(configuration)!;
            var sf = parseResult.GetValue(singleFile);
            var tr = parseResult.GetValue(trimmed);
            var outFile = parseResult.GetValue(output)!;
            var opt = optionsBinder.Bind(parseResult);

            if (string.IsNullOrWhiteSpace(ridVal) && !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.Error.WriteLine("--rid is required when building DEB from-project on non-Linux hosts (e.g., linux-x64/linux-arm64).");
                Environment.ExitCode = 1;
                return;
            }

            var publisher = new DotnetPackaging.Publish.DotnetPublisher();
            var req = new DotnetPackaging.Publish.ProjectPublishRequest(prj.FullName)
            {
                Rid = string.IsNullOrWhiteSpace(ridVal) ? Maybe<string>.None : Maybe<string>.From(ridVal!),
                SelfContained = sc,
                Configuration = cfg,
                SingleFile = sf,
                Trimmed = tr
            };

            var pub = await publisher.Publish(req);
            if (pub.IsFailure)
            {
                Console.Error.WriteLine(pub.Error);
                Environment.ExitCode = 1;
                return;
            }

            var container = pub.Value.Container;
            var name = pub.Value.Name.Match(value => value, () => (string?)null);
            var built = await Deb.DebFile.From().Container(container, name).Configure(o => o.From(opt)).Build();
            if (built.IsFailure)
            {
                Console.Error.WriteLine(built.Error);
                Environment.ExitCode = 1;
                return;
            }

            var data = DotnetPackaging.Deb.Archives.Deb.DebMixin.ToData(built.Value);
            await using var fs = outFile.Open(FileMode.Create);
            var dumpRes = await data.DumpTo(fs);
            if (dumpRes.IsFailure)
            {
                Console.Error.WriteLine(dumpRes.Error);
                Environment.ExitCode = 1;
            }
        });

        debCommand.Add(fromProject);
    }

    private static void AddMsixSubcommands(Command msixCommand)
    {
        var inputDir = new Option<DirectoryInfo>("--directory") { Description = "The input directory (publish output)", Required = true };
        var outputFile = new Option<FileInfo>("--output") { Description = "Output .msix file", Required = true };
        var packCmd = new Command("pack") { Description = "Create an MSIX from a directory (expects AppxManifest.xml in the tree or pre-baked metadata). Experimental." };
        packCmd.Add(inputDir);
        packCmd.Add(outputFile);
        packCmd.SetAction(async parseResult =>
        {
            var inDir = parseResult.GetValue(inputDir)!;
            var outFile = parseResult.GetValue(outputFile)!;
            var dirInfo = new System.IO.Abstractions.FileSystem().DirectoryInfo.New(inDir.FullName);
            var container = new Zafiro.DivineBytes.System.IO.DirectoryContainer(dirInfo);
            await DotnetPackaging.Msix.Msix.FromDirectory(container, Maybe<Serilog.ILogger>.None)
                .Bind(bytes => bytes.WriteTo(outFile.FullName))
                .WriteResult();
        });
        msixCommand.Add(packCmd);

        var project = new Option<FileInfo>("--project") { Description = "Path to the .csproj file", Required = true };
        var rid = new Option<string?>("--rid") { Description = "Runtime identifier (e.g. win-x64)" };
        var selfContained = new Option<bool>("--self-contained") { Description = "Publish self-contained" };
        selfContained.DefaultValueFactory = _ => false;
        var configuration = new Option<string>("--configuration") { Description = "Build configuration" };
        configuration.DefaultValueFactory = _ => "Release";
        var singleFile = new Option<bool>("--single-file") { Description = "Publish single-file" };
        var trimmed = new Option<bool>("--trimmed") { Description = "Enable trimming" };
        var outMsix = new Option<FileInfo>("--output") { Description = "Output .msix file", Required = true };
        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build an MSIX from the published output (expects manifest/assets)." };
        fromProject.Add(project);
        fromProject.Add(rid);
        fromProject.Add(selfContained);
        fromProject.Add(configuration);
        fromProject.Add(singleFile);
        fromProject.Add(trimmed);
        fromProject.Add(outMsix);
        fromProject.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(project)!;
            var ridVal = parseResult.GetValue(rid);
            var sc = parseResult.GetValue(selfContained);
            var cfg = parseResult.GetValue(configuration)!;
            var sf = parseResult.GetValue(singleFile);
            var tr = parseResult.GetValue(trimmed);
            var outFile = parseResult.GetValue(outMsix)!;

            if (string.IsNullOrWhiteSpace(ridVal) && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.Error.WriteLine("--rid is required when building MSIX from-project on non-Windows hosts (e.g., win-x64/win-arm64).");
                Environment.ExitCode = 1;
                return;
            }

            var publisher = new DotnetPackaging.Publish.DotnetPublisher();
            var req = new DotnetPackaging.Publish.ProjectPublishRequest(prj.FullName)
            {
                Rid = string.IsNullOrWhiteSpace(ridVal) ? Maybe<string>.None : Maybe<string>.From(ridVal!),
                SelfContained = sc,
                Configuration = cfg,
                SingleFile = sf,
                Trimmed = tr
            };
            var pub = await publisher.Publish(req);
            if (pub.IsFailure)
            {
                Console.Error.WriteLine(pub.Error);
                Environment.ExitCode = 1;
                return;
            }

            await DotnetPackaging.Msix.Msix.FromDirectory(pub.Value.Container, Maybe<Serilog.ILogger>.None)
                .Bind(bytes => bytes.WriteTo(outFile.FullName))
                .WriteResult();
        });
        msixCommand.Add(fromProject);
    }

    private static void AddAppImageFromProjectSubcommand(Command appImageCommand)
    {
        var project = new Option<FileInfo>("--project") { Description = "Path to the .csproj file", Required = true };
        var rid = new Option<string?>("--rid") { Description = "Runtime identifier (e.g. linux-x64, linux-arm64)" };
        var selfContained = new Option<bool>("--self-contained") { Description = "Publish self-contained" };
        selfContained.DefaultValueFactory = _ => true;
        var configuration = new Option<string>("--configuration") { Description = "Build configuration" };
        configuration.DefaultValueFactory = _ => "Release";
        var singleFile = new Option<bool>("--single-file") { Description = "Publish single-file" };
        var trimmed = new Option<bool>("--trimmed") { Description = "Enable trimming" };
        var output = new Option<FileInfo>("--output") { Description = "Output .AppImage file", Required = true };

        // Reuse metadata options via OptionsBinder
        var appName = new Option<string>("--application-name") { Description = "Application name", Required = false };
        var startupWmClass = new Option<string>("--wm-class") { Description = "Startup WM Class", Required = false };
        var mainCategory = new Option<MainCategory?>("--main-category") { Description = "Main category", Required = false, Arity = ArgumentArity.ZeroOrOne };
        var additionalCategories = new Option<IEnumerable<AdditionalCategory>>("--additional-categories") { Description = "Additional categories", Required = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var keywords = new Option<IEnumerable<string>>("--keywords") { Description = "Keywords", Required = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var comment = new Option<string>("--comment") { Description = "Comment", Required = false };
        var version = new Option<string>("--version") { Description = "Version", Required = false };
        var homePage = new Option<Uri>("--homepage") { Description = "Home page of the application", Required = false };
        var license = new Option<string>("--license") { Description = "License of the application", Required = false };
        var screenshotUrls = new Option<IEnumerable<Uri>>("--screenshot-urls") { Description = "Screenshot URLs", Required = false };
        var summary = new Option<string>("--summary") { Description = "Summary. Short description that should not end in a dot.", Required = false };
        var appId = new Option<string>("--appId") { Description = "Application Id. Usually a Reverse DNS name like com.SomeCompany.SomeApplication", Required = false };
        var executableName = new Option<string>("--executable-name") { Description = "Name of your application's executable", Required = false };
        var isTerminal = new Option<bool>("--is-terminal") { Description = "Indicates whether your application is a terminal application", Required = false };
        var iconOption = new Option<IIcon?>("--icon") { Required = false, Description = "Path to the application icon" };
        iconOption.CustomParser = GetIcon;

        var optionsBinder = new OptionsBinder(appName, startupWmClass, keywords, comment, mainCategory, additionalCategories, iconOption, version, homePage, license, screenshotUrls, summary, appId, executableName, isTerminal);

        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build an AppImage from the published output." };
        fromProject.Add(project);
        fromProject.Add(rid);
        fromProject.Add(selfContained);
        fromProject.Add(configuration);
        fromProject.Add(singleFile);
        fromProject.Add(trimmed);
        fromProject.Add(output);
        fromProject.Add(appName);
        fromProject.Add(startupWmClass);
        fromProject.Add(mainCategory);
        fromProject.Add(additionalCategories);
        fromProject.Add(keywords);
        fromProject.Add(comment);
        fromProject.Add(version);
        fromProject.Add(homePage);
        fromProject.Add(license);
        fromProject.Add(screenshotUrls);
        fromProject.Add(summary);
        fromProject.Add(appId);
        fromProject.Add(executableName);
        fromProject.Add(isTerminal);
        fromProject.Add(iconOption);

        fromProject.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(project)!;
            var ridVal = parseResult.GetValue(rid);
            var sc = parseResult.GetValue(selfContained);
            var cfg = parseResult.GetValue(configuration)!;
            var sf = parseResult.GetValue(singleFile);
            var tr = parseResult.GetValue(trimmed);
            var outFile = parseResult.GetValue(output)!;
            var opt = optionsBinder.Bind(parseResult);

            if (string.IsNullOrWhiteSpace(ridVal) && !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.Error.WriteLine("--rid is required when building AppImage from-project on non-Linux hosts (e.g., linux-x64/linux-arm64).");
                Environment.ExitCode = 1;
                return;
            }

            var publisher = new DotnetPackaging.Publish.DotnetPublisher();
            var req = new DotnetPackaging.Publish.ProjectPublishRequest(prj.FullName)
            {
                Rid = string.IsNullOrWhiteSpace(ridVal) ? Maybe<string>.None : Maybe<string>.From(ridVal!),
                SelfContained = sc,
                Configuration = cfg,
                SingleFile = sf,
                Trimmed = tr
            };

            var pub = await publisher.Publish(req);
            if (pub.IsFailure)
            {
                Console.Error.WriteLine(pub.Error);
                Environment.ExitCode = 1;
                return;
            }

            var root = pub.Value.Container;
            var ctxDir = new DirectoryInfo(pub.Value.OutputDirectory);
            var metadata = BuildAppImageMetadata(opt, ctxDir);
            var factory = new AppImageFactory();
            var res = await factory.Create(root, metadata)
                .Bind(x => x.ToByteSource())
                .Bind(bytes => bytes.WriteTo(outFile.FullName));
            if (res.IsFailure)
            {
                Console.Error.WriteLine(res.Error);
                Environment.ExitCode = 1;
            }
        });

        appImageCommand.Add(fromProject);
    }

    private static Result CopyRpmToOutput(FileInfo rpmFile, FileInfo outputFile)
    {
        try
        {
            var directory = outputFile.Directory;
            if (directory != null && !directory.Exists)
            {
                directory.Create();
            }

            File.Copy(rpmFile.FullName, outputFile.FullName, true);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static Task CreateDmg(DirectoryInfo inputDir, FileInfo outputFile, Options options, ILogger logger)
    {
        logger.Debug("Packaging DMG artifact from {Directory}", inputDir.FullName);
        var name = options.Name.GetValueOrDefault(inputDir.Name);
        return DotnetPackaging.Dmg.DmgIsoBuilder.Create(inputDir.FullName, outputFile.FullName, name);
    }

    private static void AddDmgFromProjectSubcommand(Command dmgCommand)
    {
        var project = new Option<FileInfo>("--project") { Description = "Path to the .csproj file", Required = true };
        var rid = new Option<string?>("--rid") { Description = "Runtime identifier (e.g. osx-x64, osx-arm64)", Required = true };
        var selfContained = new Option<bool>("--self-contained") { Description = "Publish self-contained" };
        selfContained.DefaultValueFactory = _ => true;
        var configuration = new Option<string>("--configuration") { Description = "Build configuration" };
        configuration.DefaultValueFactory = _ => "Release";
        var singleFile = new Option<bool>("--single-file") { Description = "Publish single-file" };
        var trimmed = new Option<bool>("--trimmed") { Description = "Enable trimming" };
        var output = new Option<FileInfo>("--output") { Description = "Output .dmg file", Required = true };

        // Reuse metadata options to get volume name from --application-name if present
        var appName = new Option<string>("--application-name") { Description = "Application name / volume name", Required = false };
        var dmgIconOption = new Option<IIcon?>("--icon") { Description = "Path to the application icon" };
        dmgIconOption.CustomParser = GetIcon;

        var optionsBinder = new OptionsBinder(appName,
            new Option<string>("--wm-class"),
            new Option<IEnumerable<string>>("--keywords"),
            new Option<string>("--comment"),
            new Option<MainCategory?>("--main-category"),
            new Option<IEnumerable<AdditionalCategory>>("--additional-categories"),
            dmgIconOption,
            new Option<string>("--version"),
            new Option<Uri>("--homepage"),
            new Option<string>("--license"),
            new Option<IEnumerable<Uri>>("--screenshot-urls"),
            new Option<string>("--summary"),
            new Option<string>("--appId"),
            new Option<string>("--executable-name"),
            new Option<bool>("--is-terminal"));

        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build a .dmg from the published output (.app bundle auto-generated if missing). Experimental." };
        fromProject.Add(project);
        fromProject.Add(rid);
        fromProject.Add(selfContained);
        fromProject.Add(configuration);
        fromProject.Add(singleFile);
        fromProject.Add(trimmed);
        fromProject.Add(output);
        fromProject.Add(appName);

        fromProject.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(project)!;
            var ridVal = parseResult.GetValue(rid);
            var sc = parseResult.GetValue(selfContained);
            var cfg = parseResult.GetValue(configuration)!;
            var sf = parseResult.GetValue(singleFile);
            var tr = parseResult.GetValue(trimmed);
            var outFile = parseResult.GetValue(output)!;
            var opt = optionsBinder.Bind(parseResult);

            var publisher = new DotnetPackaging.Publish.DotnetPublisher();
            var req = new DotnetPackaging.Publish.ProjectPublishRequest(prj.FullName)
            {
                Rid = string.IsNullOrWhiteSpace(ridVal) ? Maybe<string>.None : Maybe<string>.From(ridVal!),
                SelfContained = sc,
                Configuration = cfg,
                SingleFile = sf,
                Trimmed = tr
            };

            var pub = await publisher.Publish(req);
            if (pub.IsFailure)
            {
                Console.Error.WriteLine(pub.Error);
                Environment.ExitCode = 1;
                return;
            }

            var volName = opt.Name.GetValueOrDefault(pub.Value.Name.GetValueOrDefault("App"));
            await DotnetPackaging.Dmg.DmgIsoBuilder.Create(pub.Value.OutputDirectory, outFile.FullName, volName);
            Log.Information("Success");
        });

        dmgCommand.Add(fromProject);
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
            result.AddError($"Invalid icon '{iconPath}': {dataResult.Error}");
        }

        // For now, do not eagerly parse the icon (async). We rely on auto-detection or later stages.
        return null;
    }
    private sealed class FlatpakOptionsBinder
    {
        private readonly Option<string> runtime;
        private readonly Option<string> sdk;
        private readonly Option<string> branch;
        private readonly Option<string> runtimeVersion;
        private readonly Option<IEnumerable<string>> shared;
        private readonly Option<IEnumerable<string>> sockets;
        private readonly Option<IEnumerable<string>> devices;
        private readonly Option<IEnumerable<string>> filesystems;
        private readonly Option<string?> arch;
        private readonly Option<string?> command;

        public FlatpakOptionsBinder(
            Option<string> runtime,
            Option<string> sdk,
            Option<string> branch,
            Option<string> runtimeVersion,
            Option<IEnumerable<string>> shared,
            Option<IEnumerable<string>> sockets,
            Option<IEnumerable<string>> devices,
            Option<IEnumerable<string>> filesystems,
            Option<string?> arch,
            Option<string?> command)
        {
            this.runtime = runtime;
            this.sdk = sdk;
            this.branch = branch;
            this.runtimeVersion = runtimeVersion;
            this.shared = shared;
            this.sockets = sockets;
            this.devices = devices;
            this.filesystems = filesystems;
            this.arch = arch;
            this.command = command;
        }

        public FlatpakOptions Bind(ParseResult parseResult)
        {
            var archStr = parseResult.GetValue(arch);
            var parsedArch = string.IsNullOrWhiteSpace(archStr) ? null : ParseArchitecture(archStr!);
            return new FlatpakOptions
            {
                Runtime = parseResult.GetValue(runtime)!,
                Sdk = parseResult.GetValue(sdk)!,
                Branch = parseResult.GetValue(branch)!,
                RuntimeVersion = parseResult.GetValue(runtimeVersion)!,
                Shared = parseResult.GetValue(shared)!,
                Sockets = parseResult.GetValue(sockets)!,
                Devices = parseResult.GetValue(devices)!,
                Filesystems = parseResult.GetValue(filesystems)!,
                ArchitectureOverride = parsedArch == null ? Maybe<Architecture>.None : Maybe<Architecture>.From(parsedArch),
                CommandOverride = parseResult.GetValue(command) is { } s && !string.IsNullOrWhiteSpace(s) ? Maybe<string>.From(s) : Maybe<string>.None
            };
        }
    }

    private static Architecture? ParseArchitecture(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        return v switch
        {
            "x86_64" or "amd64" or "x64" => Architecture.X64,
            "aarch64" or "arm64" => Architecture.Arm64,
            "i386" or "x86" => Architecture.X86,
            "armhf" or "arm32" => Architecture.Arm32,
            _ => null
        };
    }
}
