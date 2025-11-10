using System.CommandLine;
using System.CommandLine.Parsing;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage;
using System.Runtime.InteropServices;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Metadata;
using DotnetPackaging.Deb;
using DotnetPackaging.Flatpak;
using DotnetPackaging.Rpm;
using Serilog;
using Zafiro.DataModel;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using Zafiro.FileSystem.Core;
using System.Diagnostics;
using DotnetPackaging.Exe;

namespace DotnetPackaging.Tool;

static class Program
{
    private static readonly System.IO.Abstractions.FileSystem FileSystem = new();
    
    public static Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {Module}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        
        var rootCommand = new RootCommand
        {
            Description = "Package published .NET applications into Linux-friendly formats.\n\n" +
                          "Available verbs:\n" +
                          "- deb: Build a Debian/Ubuntu .deb installer.\n" +
                          "- rpm: Build an RPM (.rpm) package for Fedora, openSUSE and similar distributions.\n" +
                          "- appimage: Build a portable AppImage (.AppImage) bundle or work with AppDir workflows.\n\n" +
                          "Tip: run `dotnetpackaging <verb> --help` to see format-specific options."
        };

        var debCommand = CreateCommand(
            "deb",
            "Debian package",
            ".deb",
            CreateDeb,
            "Create a Debian (.deb) installer for Debian and Ubuntu based distributions.",
            "pack-deb",
            "debian");
        AddDebFromProjectSubcommand(debCommand);
        rootCommand.AddCommand(debCommand);

        var rpmCommand = CreateCommand(
            "rpm",
            "RPM package",
            ".rpm",
            CreateRpm,
            "Create an RPM (.rpm) package suitable for Fedora, openSUSE, and other RPM-based distributions.",
            "pack-rpm");
        // Add rpm from-project subcommand
        AddRpmFromProjectSubcommand(rpmCommand);
        rootCommand.AddCommand(rpmCommand);

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
        rootCommand.AddCommand(appImageCommand);

        // DMG command (experimental, cross-platform)
        var dmgCommand = CreateCommand(
            "dmg",
            "macOS disk image",
            ".dmg",
            CreateDmg,
            "Create a simple macOS disk image (.dmg). Currently uses an ISO/UDF (UDTO) payload for broad compatibility.",
            "pack-dmg");
        AddDmgFromProjectSubcommand(dmgCommand);
        rootCommand.AddCommand(dmgCommand);

        // dmg verify subcommand
        var verifyCmd = new Command("verify", "Verify that a .dmg file has a macOS-friendly structure (ISO/UDTO or UDIF).")
        {
            new Option<FileInfo>("--file", "Path to the .dmg file") { IsRequired = true }
        };
        verifyCmd.SetHandler(async (FileInfo file) =>
        {
            var result = await DotnetPackaging.Dmg.DmgVerifier.Verify(file.FullName);
            if (result.IsFailure)
            {
                Console.Error.WriteLine(result.Error);
                Environment.ExitCode = 1;
            }
            else
            {
                Log.Information("{VerificationResult}", result.Value);
            }
        }, verifyCmd.Options.OfType<Option<FileInfo>>().First());
        dmgCommand.AddCommand(verifyCmd);

        // Flatpak command
        var flatpakCommand = new Command("flatpak", "Flatpak packaging: generate layout, OSTree repo, or bundle (.flatpak). Can use system flatpak or internal bundler.");
        AddFlatpakSubcommands(flatpakCommand);
        AddFlatpakFromProjectSubcommand(flatpakCommand);
        rootCommand.AddCommand(flatpakCommand);

        // MSIX command (experimental)
        var msixCommand = new Command("msix", "MSIX packaging (experimental)");
        AddMsixSubcommands(msixCommand);
        rootCommand.AddCommand(msixCommand);

        // EXE SFX command
        var exeCommand = new Command("exe", "Windows self-extracting installer (.exe). If --stub is not provided, the tool downloads the appropriate stub from GitHub Releases.");
        var exeInputDir = new Option<DirectoryInfo>("--directory", "The input directory (publish output)") { IsRequired = true };
        var exeOutput = new Option<FileInfo>("--output", "Output installer .exe") { IsRequired = true };
        var stubPath = new Option<FileInfo>("--stub", "Path to the prebuilt stub (WinExe) to concatenate (optional if repo layout is present)");
        var exRidTop = new Option<string?>("--rid", "Runtime identifier for the stub (win-x64, win-arm64)");

        // Reuse metadata options
        var exAppName = new Option<string>("--application-name", "Application name") { IsRequired = false };
        var exComment = new Option<string>("--comment", "Comment / long description") { IsRequired = false };
        var exVersion = new Option<string>("--version", "Version") { IsRequired = false };
        var exAppId = new Option<string>("--appId", "Application Id (Reverse DNS typical)") { IsRequired = false };
        var exVendor = new Option<string>("--vendor", "Vendor/Publisher") { IsRequired = false };
        var exExecutableName = new Option<string>("--executable-name", "Name of your application's executable") { IsRequired = false };
        var optionsBinder = new OptionsBinder(
            exAppName,
            new Option<string>("--wm-class"),
            new Option<IEnumerable<string>>("--keywords"),
            exComment,
            new Option<MainCategory?>("--main-category"),
            new Option<IEnumerable<AdditionalCategory>>("--additional-categories"),
            new Option<IIcon?>("--icon", GetIcon),
            exVersion,
            new Option<Uri>("--homepage"),
            new Option<string>("--license"),
            new Option<IEnumerable<Uri>>("--screenshot-urls"),
            new Option<string>("--summary"),
            exAppId,
            exExecutableName,
            new Option<bool>("--is-terminal")
        );

        exeCommand.AddOption(exeInputDir);
        exeCommand.AddOption(exeOutput);
        exeCommand.AddOption(stubPath);
        // Make metadata options global so subcommands can use them without re-adding
        exeCommand.AddGlobalOption(exAppName);
        exeCommand.AddGlobalOption(exComment);
        exeCommand.AddGlobalOption(exVersion);
        exeCommand.AddGlobalOption(exAppId);
        exeCommand.AddGlobalOption(exVendor);
        exeCommand.AddGlobalOption(exExecutableName);
        exeCommand.AddOption(exRidTop);

        var exeService = new ExePackagingService();
        exeCommand.SetHandler(async (DirectoryInfo inDir, FileInfo outFile, FileInfo? stub, Options opt, string? vendorOpt, string? ridOpt) =>
        {
            var result = await exeService.BuildFromDirectory(inDir, outFile, opt, vendorOpt, ridOpt, stub);
            if (result.IsFailure)
            {
                Console.Error.WriteLine(result.Error);
                Environment.ExitCode = 1;
                return;
            }

            Log.Information("{OutputFile}", result.Value.FullName);
        }, exeInputDir, exeOutput, stubPath, optionsBinder, exVendor, exRidTop);

        // exe from-project
        var exProject = new Option<FileInfo>("--project", "Path to the .csproj file") { IsRequired = true };
        var exRid = new Option<string?>("--rid", "Runtime identifier (e.g. win-x64, win-arm64)");
        var exSelfContained = new Option<bool>("--self-contained", () => true, "Publish self-contained");
        var exConfiguration = new Option<string>("--configuration", () => "Release", "Build configuration");
        var exSingleFile = new Option<bool>("--single-file", "Publish single-file");
        var exTrimmed = new Option<bool>("--trimmed", "Enable trimming");
        var exOut = new Option<FileInfo>("--output", "Output installer .exe") { IsRequired = true };
        var exStub = new Option<FileInfo>("--stub", "Path to the prebuilt stub (WinExe) to concatenate (optional if repo layout is present)");

        var exFromProject = new Command("from-project", "Publish a .NET project and build a Windows self-extracting installer (.exe). If --stub is not provided, the tool downloads the appropriate stub from GitHub Releases.");
        exFromProject.AddOption(exProject);
        exFromProject.AddOption(exRid);
        exFromProject.AddOption(exSelfContained);
        exFromProject.AddOption(exConfiguration);
        exFromProject.AddOption(exSingleFile);
        exFromProject.AddOption(exTrimmed);
        exFromProject.AddOption(exOut);
        exFromProject.AddOption(exStub);

        // Use a compact binder to avoid exceeding SetHandler's supported parameter count
        var exExtrasBinder = new ExeFromProjectExtraBinder(exOut, exStub, exVendor);
        exFromProject.SetHandler(async (FileInfo prj, string? ridVal, bool sc, string cfg, bool sf, bool tr, Options opt, ExeFromProjectExtra extras) =>
        {
            var result = await exeService.BuildFromProject(prj, ridVal, sc, cfg, sf, tr, extras.Output, opt, extras.Vendor, extras.Stub);
            if (result.IsFailure)
            {
                Console.Error.WriteLine(result.Error);
                Environment.ExitCode = 1;
                return;
            }

            Log.Information("{OutputFile}", result.Value.FullName);
        }, exProject, exRid, exSelfContained, exConfiguration, exSingleFile, exTrimmed, optionsBinder, exExtrasBinder);

        exeCommand.AddCommand(exFromProject);

        rootCommand.AddCommand(exeCommand);
        
        return rootCommand.InvokeAsync(args);
    }

    private static Command CreateCommand(
        string commandName,
        string friendlyName,
        string extension,
        Func<DirectoryInfo, FileInfo, Options, Task> handler,
        string? description = null,
        params string[] aliases)
    {
        var buildDir = new Option<DirectoryInfo>("--directory", "Published application directory (for example: bin/Release/<tfm>/publish)") { IsRequired = true };
        var outputFileOption = new Option<FileInfo>("--output", $"Destination path for the generated {extension} file") { IsRequired = true };
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

        var defaultDescription = description ??
                                 $"Create a {friendlyName} from a directory with the published application contents. Everything is inferred. For .NET apps this is usually the 'publish' directory.";
        var fromBuildDir = new Command(commandName, defaultDescription);

        foreach (var alias in aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                fromBuildDir.AddAlias(alias);
            }
        }

        fromBuildDir.AddOption(buildDir);
        fromBuildDir.AddOption(outputFileOption);
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
        
        fromBuildDir.SetHandler(handler, buildDir, outputFileOption, options);
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

    private static void AddFlatpakSubcommands(Command flatpakCommand)
    {
        // Options reused for metadata
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

        // Flatpak-specific options
        var fpRuntime = new Option<string>("--runtime", () => "org.freedesktop.Platform", "Flatpak runtime (e.g. org.freedesktop.Platform)");
        var fpSdk = new Option<string>("--sdk", () => "org.freedesktop.Sdk", "Flatpak SDK (e.g. org.freedesktop.Sdk)");
        var fpBranch = new Option<string>("--branch", () => "stable", "Flatpak branch (e.g. stable)");
        var fpRuntimeVersion = new Option<string>("--runtime-version", () => "23.08", "Flatpak runtime version");
        var fpShared = new Option<IEnumerable<string>>("--shared", () => new[] { "network", "ipc" }, "Flatpak [Context] shared permissions") { AllowMultipleArgumentsPerToken = true };
        var fpSockets = new Option<IEnumerable<string>>("--sockets", () => new[] { "wayland", "x11", "pulseaudio" }, "Flatpak [Context] sockets") { AllowMultipleArgumentsPerToken = true };
        var fpDevices = new Option<IEnumerable<string>>("--devices", () => new[] { "dri" }, "Flatpak [Context] devices") { AllowMultipleArgumentsPerToken = true };
        var fpFilesystems = new Option<IEnumerable<string>>("--filesystems", () => new[] { "home" }, "Flatpak [Context] filesystems") { AllowMultipleArgumentsPerToken = true };
        var fpArch = new Option<string?>("--arch", "Override architecture (x86_64, aarch64, i386, armhf)");
        var fpCommandOverride = new Option<string?>("--command", "Override command name inside Flatpak (defaults to AppId)");

        var inputDir = new Option<DirectoryInfo>("--directory", "The input directory (publish output)") { IsRequired = true };
        var outputDir = new Option<DirectoryInfo>("--output-dir", "Destination directory for the Flatpak layout") { IsRequired = true };
        var layoutCmd = new Command("layout", "Creates a Flatpak layout (metadata, files/) from a published directory.");
        layoutCmd.AddOption(inputDir);
        layoutCmd.AddOption(outputDir);
        layoutCmd.AddOption(appName);
        layoutCmd.AddOption(startupWmClass);
        layoutCmd.AddOption(mainCategory);
        layoutCmd.AddOption(additionalCategories);
        layoutCmd.AddOption(keywords);
        layoutCmd.AddOption(comment);
        layoutCmd.AddOption(version);
        layoutCmd.AddOption(homePage);
        layoutCmd.AddOption(license);
        layoutCmd.AddOption(screenshotUrls);
        layoutCmd.AddOption(summary);
        layoutCmd.AddOption(appId);
        layoutCmd.AddOption(executableName);
        layoutCmd.AddOption(isTerminal);
        layoutCmd.AddOption(iconOption);
        layoutCmd.AddOption(fpRuntime);
        layoutCmd.AddOption(fpSdk);
        layoutCmd.AddOption(fpBranch);
        layoutCmd.AddOption(fpRuntimeVersion);
        layoutCmd.AddOption(fpShared);
        layoutCmd.AddOption(fpSockets);
        layoutCmd.AddOption(fpDevices);
        layoutCmd.AddOption(fpFilesystems);
        layoutCmd.AddOption(fpArch);
        layoutCmd.AddOption(fpCommandOverride);
        var fpBinder = new FlatpakOptionsBinder(fpRuntime, fpSdk, fpBranch, fpRuntimeVersion, fpShared, fpSockets, fpDevices, fpFilesystems, fpArch, fpCommandOverride);
        layoutCmd.SetHandler(CreateFlatpakLayout, inputDir, outputDir, binder, fpBinder);

        flatpakCommand.AddCommand(layoutCmd);

        var bundleInputDir = new Option<DirectoryInfo>("--directory", "The input directory (publish output)") { IsRequired = true };
        var bundleOutput = new Option<FileInfo>("--output", "Output .flatpak file") { IsRequired = true };
        var bundleCmd = new Command("bundle", "Creates a single-file .flatpak bundle from a published directory. By default uses internal bundler; pass --system to use installed flatpak.");
        bundleCmd.AddOption(bundleInputDir);
        bundleCmd.AddOption(bundleOutput);
        var useSystem = new Option<bool>("--system", "Use system 'flatpak' (build-export/build-bundle) if available");
        bundleCmd.AddOption(useSystem);
        bundleCmd.AddOption(appName);
        bundleCmd.AddOption(startupWmClass);
        bundleCmd.AddOption(mainCategory);
        bundleCmd.AddOption(additionalCategories);
        bundleCmd.AddOption(keywords);
        bundleCmd.AddOption(comment);
        bundleCmd.AddOption(version);
        bundleCmd.AddOption(homePage);
        bundleCmd.AddOption(license);
        bundleCmd.AddOption(screenshotUrls);
        bundleCmd.AddOption(summary);
        bundleCmd.AddOption(appId);
        bundleCmd.AddOption(executableName);
        bundleCmd.AddOption(isTerminal);
        bundleCmd.AddOption(iconOption);
        bundleCmd.AddOption(fpRuntime);
        bundleCmd.AddOption(fpSdk);
        bundleCmd.AddOption(fpBranch);
        bundleCmd.AddOption(fpRuntimeVersion);
        bundleCmd.AddOption(fpShared);
        bundleCmd.AddOption(fpSockets);
        bundleCmd.AddOption(fpDevices);
        bundleCmd.AddOption(fpFilesystems);
        bundleCmd.AddOption(fpArch);
        bundleCmd.AddOption(fpCommandOverride);
        bundleCmd.SetHandler(CreateFlatpakBundle, bundleInputDir, bundleOutput, useSystem, binder, fpBinder);

        flatpakCommand.AddCommand(bundleCmd);

        // flatpak from-project (bundle)
        var fpPrj = new Option<FileInfo>("--project", "Path to the .csproj file") { IsRequired = true };
        var fpOut = new Option<FileInfo>("--output", "Output .flatpak file") { IsRequired = true };
        var fpUseSystem = new Option<bool>("--system", "Use system 'flatpak' (build-export/build-bundle) if available");
        var fromProjectCmd = new Command("from-project", "Publish a .NET project and build a .flatpak bundle.");
        fromProjectCmd.AddOption(fpPrj);
        fromProjectCmd.AddOption(fpOut);
        fromProjectCmd.AddOption(fpUseSystem);
        fromProjectCmd.AddOption(appName);
        fromProjectCmd.AddOption(startupWmClass);
        fromProjectCmd.AddOption(mainCategory);
        fromProjectCmd.AddOption(additionalCategories);
        fromProjectCmd.AddOption(keywords);
        fromProjectCmd.AddOption(comment);
        fromProjectCmd.AddOption(version);
        fromProjectCmd.AddOption(homePage);
        fromProjectCmd.AddOption(license);
        fromProjectCmd.AddOption(screenshotUrls);
        fromProjectCmd.AddOption(summary);
        fromProjectCmd.AddOption(appId);
        fromProjectCmd.AddOption(executableName);
        fromProjectCmd.AddOption(isTerminal);
        fromProjectCmd.AddOption(iconOption);
        fromProjectCmd.AddOption(fpRuntime);
        fromProjectCmd.AddOption(fpSdk);
        fromProjectCmd.AddOption(fpBranch);
        fromProjectCmd.AddOption(fpRuntimeVersion);
        fromProjectCmd.AddOption(fpShared);
        fromProjectCmd.AddOption(fpSockets);
        fromProjectCmd.AddOption(fpDevices);
        fromProjectCmd.AddOption(fpFilesystems);
        fromProjectCmd.AddOption(fpArch);
        fromProjectCmd.AddOption(fpCommandOverride);
        fromProjectCmd.SetHandler(async (FileInfo prj, FileInfo outFile, bool useSystemBundler, Options opt, FlatpakOptions fopt) =>
        {
            var publisher = new DotnetPackaging.Publish.DotnetPublisher();
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
                Console.Error.WriteLine(pub.Error);
                Environment.ExitCode = 1;
                return;
            }

            var root = pub.Value.Container;
            var setup = new FromDirectoryOptions();
            setup.From(opt);

            var execRes = await BuildUtils.GetExecutable(root, setup);
            if (execRes.IsFailure) { Console.Error.WriteLine(execRes.Error); return; }
            var archRes = await BuildUtils.GetArch(setup, execRes.Value);
            if (archRes.IsFailure) { Console.Error.WriteLine(archRes.Error); return; }
            var pm = await BuildUtils.CreateMetadata(setup, root, archRes.Value, execRes.Value, opt.IsTerminal.GetValueOrDefault(false), pub.Value.Name);
            var planRes = await new FlatpakFactory().BuildPlan(root, pm, fopt);
            if (planRes.IsFailure) { Console.Error.WriteLine(planRes.Error); return; }
            var plan = planRes.Value;

            if (!useSystemBundler)
            {
                var internalBytes = FlatpakBundle.CreateOstree(plan);
                if (internalBytes.IsFailure) { Console.Error.WriteLine(internalBytes.Error); return; }
                var wr = await internalBytes.Value.WriteTo(outFile.FullName);
                if (wr.IsFailure) { Console.Error.WriteLine(wr.Error); return; }
                Log.Information("{OutputFile}", outFile.FullName);
                return;
            }

            var tmpAppDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-flatpak-app-" + Guid.NewGuid());
            var tmpRepoDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-flatpak-repo-" + Guid.NewGuid());
            Directory.CreateDirectory(tmpAppDir);
            Directory.CreateDirectory(tmpRepoDir);
            var write = await plan.ToRootContainer().WriteTo(tmpAppDir);
            if (write.IsFailure) { Console.Error.WriteLine(write.Error); return; }
            MakeExecutable(System.IO.Path.Combine(tmpAppDir, "files", "bin", plan.CommandName));
            MakeExecutable(System.IO.Path.Combine(tmpAppDir, "files", plan.ExecutableTargetPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
            var effArch = fopt.ArchitectureOverride.GetValueOrDefault(plan.Metadata.Architecture).PackagePrefix;
            var cmd = fopt.CommandOverride.GetValueOrDefault(plan.CommandName);
            var finish = Run("flatpak", $"build-finish \"{tmpAppDir}\" --command={cmd} {string.Join(" ", fopt.Shared.Select(s => $"--share={s}"))} {string.Join(" ", fopt.Sockets.Select(s => $"--socket={s}"))} {string.Join(" ", fopt.Devices.Select(d => $"--device={d}"))} {string.Join(" ", fopt.Filesystems.Select(f => $"--filesystem={f}"))}");
            if (finish.IsFailure) { Console.Error.WriteLine(finish.Error); return; }
            var export = Run("flatpak", $"build-export --arch={effArch} \"{tmpRepoDir}\" \"{tmpAppDir}\" {fopt.Branch}");
            if (export.IsFailure) { Console.Error.WriteLine(export.Error); return; }
            var bundle = Run("flatpak", $"build-bundle \"{tmpRepoDir}\" \"{outFile.FullName}\" {plan.AppId} {fopt.Branch} --arch={effArch}");
            if (bundle.IsFailure) { Console.Error.WriteLine(bundle.Error); return; }
            Log.Information("{OutputFile}", outFile.FullName);
        }, fpPrj, fpOut, fpUseSystem, binder, fpBinder);

        flatpakCommand.AddCommand(fromProjectCmd);

        var repoInputDir = new Option<DirectoryInfo>("--directory", "The input directory (publish output)") { IsRequired = true };
        var repoOutDir = new Option<DirectoryInfo>("--output-dir", "Destination directory for the OSTree repo") { IsRequired = true };
        var repoCmd = new Command("repo", "Creates an OSTree repo directory from a published directory (debug/validation).");
        repoCmd.AddOption(repoInputDir);
        repoCmd.AddOption(repoOutDir);
        repoCmd.AddOption(appName);
        repoCmd.AddOption(startupWmClass);
        repoCmd.AddOption(mainCategory);
        repoCmd.AddOption(additionalCategories);
        repoCmd.AddOption(keywords);
        repoCmd.AddOption(comment);
        repoCmd.AddOption(version);
        repoCmd.AddOption(homePage);
        repoCmd.AddOption(license);
        repoCmd.AddOption(screenshotUrls);
        repoCmd.AddOption(summary);
        repoCmd.AddOption(appId);
        repoCmd.AddOption(executableName);
        repoCmd.AddOption(isTerminal);
        repoCmd.AddOption(iconOption);
        repoCmd.AddOption(fpRuntime);
        repoCmd.AddOption(fpSdk);
        repoCmd.AddOption(fpBranch);
        repoCmd.AddOption(fpRuntimeVersion);
        repoCmd.AddOption(fpShared);
        repoCmd.AddOption(fpSockets);
        repoCmd.AddOption(fpDevices);
        repoCmd.AddOption(fpFilesystems);
        repoCmd.AddOption(fpArch);
        repoCmd.AddOption(fpCommandOverride);
        repoCmd.SetHandler(CreateFlatpakRepo, repoInputDir, repoOutDir, binder, fpBinder);

        flatpakCommand.AddCommand(repoCmd);

        // flatpak pack (minimal UX): only directory and output-dir, prefer system bundler with fallback
        var packInputDir = new Option<DirectoryInfo>("--directory", "The input directory (publish output)") { IsRequired = true };
        var packOutputDir = new Option<DirectoryInfo>("--output-dir", "Destination directory for the resulting .flatpak") { IsRequired = true };
        var packCmd = new Command("pack", "Pack a .flatpak with minimal parameters. Uses system flatpak if available, otherwise falls back to internal bundler.");
        packCmd.AddOption(packInputDir);
        packCmd.AddOption(packOutputDir);
        packCmd.SetHandler(async (DirectoryInfo inDir, DirectoryInfo outDir) =>
        {
            var dirInfo = FileSystem.DirectoryInfo.New(inDir.FullName);
            var container = new DirectoryContainer(dirInfo);
            var root = container.AsRoot();
            var setup = new FromDirectoryOptions();

            var execRes = await BuildUtils.GetExecutable(root, setup);
            if (execRes.IsFailure) { Console.Error.WriteLine(execRes.Error); return; }
            var archRes = await BuildUtils.GetArch(setup, execRes.Value);
            if (archRes.IsFailure) { Console.Error.WriteLine(archRes.Error); return; }
            var pm = await BuildUtils.CreateMetadata(setup, root, archRes.Value, execRes.Value, setup.IsTerminal, Maybe<string>.From(inDir.Name));
            var planRes = await new FlatpakFactory().BuildPlan(root, pm, new FlatpakOptions());
            if (planRes.IsFailure) { Console.Error.WriteLine(planRes.Error); return; }
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
                            Log.Information("{OutputFile}", outPath);
                            return;
                        }
                    }
                }
            }

            // Fallback: internal bundler
            var internalBytes = FlatpakBundle.CreateOstree(plan);
            if (internalBytes.IsFailure)
            {
                Console.Error.WriteLine(internalBytes.Error);
                return;
            }
            var writeRes = await internalBytes.Value.WriteTo(outPath);
            if (writeRes.IsFailure)
            {
                Console.Error.WriteLine(writeRes.Error);
            }
            else
            {
                Log.Information("{OutputFile}", outPath);
            }
        }, packInputDir, packOutputDir);
        
        flatpakCommand.AddCommand(packCmd);
    }

    private static Task CreateAppDir(DirectoryInfo inputDir, DirectoryInfo outputDir, Options options)
    {
        var dirInfo = FileSystem.DirectoryInfo.New(inputDir.FullName);
        var container = new DirectoryContainer(dirInfo);
        var root = container.AsRoot();

        var metadata = BuildAppImageMetadata(options, inputDir);
        var factory = new AppImageFactory();

        return factory.BuildAppDir(root, metadata)
            .Bind(rootDir => rootDir.WriteTo(outputDir.FullName))
            .WriteResult();
    }

    private static Task CreateFlatpakLayout(DirectoryInfo inputDir, DirectoryInfo outputDir, Options options, FlatpakOptions flatpakOptions)
    {
        var dirInfo = FileSystem.DirectoryInfo.New(inputDir.FullName);
        var container = new DirectoryContainer(dirInfo);
        var root = container.AsRoot();

        // Build generic metadata from directory
        var setup = new FromDirectoryOptions();
        setup.From(options);

        return BuildUtils.GetExecutable(root, setup)
            .Bind(exec => BuildUtils.GetArch(setup, exec).Map(a => (exec, a)))
            .Bind(async tuple =>
            {
                var pm = await BuildUtils.CreateMetadata(setup, root, tuple.a, tuple.exec, options.IsTerminal.GetValueOrDefault(false), Maybe<string>.From(inputDir.Name));
                return Result.Success(pm);
            })
            .Bind(packageMetadata => new FlatpakFactory().BuildPlan(root, packageMetadata, flatpakOptions))
            .Bind(plan => plan.ToRootContainer().WriteTo(outputDir.FullName))
            .WriteResult();
    }

    private static Task CreateFlatpakBundle(DirectoryInfo inputDir, FileInfo outputFile, bool useSystem, Options options, FlatpakOptions flatpakOptions)
    {
        var dirInfo = FileSystem.DirectoryInfo.New(inputDir.FullName);
        var container = new DirectoryContainer(dirInfo);
        var root = container.AsRoot();

        var setup = new FromDirectoryOptions();
        setup.From(options);

        return BuildUtils.GetExecutable(root, setup)
            .Bind(exec => BuildUtils.GetArch(setup, exec).Map(a => (exec, a)))
            .Bind(async tuple =>
            {
                var pm = await BuildUtils.CreateMetadata(setup, root, tuple.a, tuple.exec, options.IsTerminal.GetValueOrDefault(false), Maybe<string>.From(inputDir.Name));
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

    private static Task CreateFlatpakRepo(DirectoryInfo inputDir, DirectoryInfo outputDir, Options options, FlatpakOptions flatpakOptions)
    {
        var dirInfo = FileSystem.DirectoryInfo.New(inputDir.FullName);
        var container = new DirectoryContainer(dirInfo);
        var root = container.AsRoot();

        var setup = new FromDirectoryOptions();
        setup.From(options);

        return BuildUtils.GetExecutable(root, setup)
            .Bind(exec => BuildUtils.GetArch(setup, exec).Map(a => (exec, a)))
            .Bind(async tuple =>
            {
                var pm = await BuildUtils.CreateMetadata(setup, root, tuple.a, tuple.exec, options.IsTerminal.GetValueOrDefault(false), Maybe<string>.From(inputDir.Name));
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

    private static Task CreateRpm(DirectoryInfo inputDir, FileInfo outputFile, Options options)
    {
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
        var project = new Option<FileInfo>("--project", "Path to the .csproj file") { IsRequired = true };
        var rid = new Option<string?>("--rid", "Runtime identifier (e.g. linux-x64, linux-arm64)");
        var selfContained = new Option<bool>("--self-contained", () => true, "Publish self-contained");
        var configuration = new Option<string>("--configuration", () => "Release", "Build configuration");
        var singleFile = new Option<bool>("--single-file", "Publish single-file");
        var trimmed = new Option<bool>("--trimmed", "Enable trimming");
        var output = new Option<FileInfo>("--output", "Destination path for the generated .rpm") { IsRequired = true };

        // Reuse metadata options via OptionsBinder
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
        var iconOption = new Option<IIcon?>("--icon", GetIcon ) { IsRequired = false, Description = "Path to the application icon" };

        var optionsBinder = new OptionsBinder(appName, startupWmClass, keywords, comment, mainCategory, additionalCategories, iconOption, version, homePage, license, screenshotUrls, summary, appId, executableName, isTerminal);

        var fromProject = new Command("from-project", "Publish a .NET project and build an RPM from the published output (no code duplication; library drives the pipeline).");
        fromProject.AddOption(project);
        fromProject.AddOption(rid);
        fromProject.AddOption(selfContained);
        fromProject.AddOption(configuration);
        fromProject.AddOption(singleFile);
        fromProject.AddOption(trimmed);
        fromProject.AddOption(output);
        fromProject.AddOption(appName);
        fromProject.AddOption(startupWmClass);
        fromProject.AddOption(mainCategory);
        fromProject.AddOption(additionalCategories);
        fromProject.AddOption(keywords);
        fromProject.AddOption(comment);
        fromProject.AddOption(version);
        fromProject.AddOption(homePage);
        fromProject.AddOption(license);
        fromProject.AddOption(screenshotUrls);
        fromProject.AddOption(summary);
        fromProject.AddOption(appId);
        fromProject.AddOption(executableName);
        fromProject.AddOption(isTerminal);
        fromProject.AddOption(iconOption);

        fromProject.SetHandler(async (FileInfo prj, string? ridVal, bool sc, string cfg, bool sf, bool tr, FileInfo outFile, Options opt) =>
        {
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
            var name = pub.Value.Name.GetValueOrDefault(null);
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
        }, project, rid, selfContained, configuration, singleFile, trimmed, output, optionsBinder);

        rpmCommand.AddCommand(fromProject);
    }

    private static void AddDebFromProjectSubcommand(Command debCommand)
    {
        var project = new Option<FileInfo>("--project", "Path to the .csproj file") { IsRequired = true };
        var rid = new Option<string?>("--rid", "Runtime identifier (e.g. linux-x64, linux-arm64)");
        var selfContained = new Option<bool>("--self-contained", () => true, "Publish self-contained");
        var configuration = new Option<string>("--configuration", () => "Release", "Build configuration");
        var singleFile = new Option<bool>("--single-file", "Publish single-file");
        var trimmed = new Option<bool>("--trimmed", "Enable trimming");
        var output = new Option<FileInfo>("--output", "Destination path for the generated .deb") { IsRequired = true };

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
        var iconOption = new Option<IIcon?>("--icon", GetIcon ) { IsRequired = false, Description = "Path to the application icon" };

        var optionsBinder = new OptionsBinder(appName, startupWmClass, keywords, comment, mainCategory, additionalCategories, iconOption, version, homePage, license, screenshotUrls, summary, appId, executableName, isTerminal);

        var fromProject = new Command("from-project", "Publish a .NET project and build a Debian .deb from the published output.");
        fromProject.AddOption(project);
        fromProject.AddOption(rid);
        fromProject.AddOption(selfContained);
        fromProject.AddOption(configuration);
        fromProject.AddOption(singleFile);
        fromProject.AddOption(trimmed);
        fromProject.AddOption(output);
        fromProject.AddOption(appName);
        fromProject.AddOption(startupWmClass);
        fromProject.AddOption(mainCategory);
        fromProject.AddOption(additionalCategories);
        fromProject.AddOption(keywords);
        fromProject.AddOption(comment);
        fromProject.AddOption(version);
        fromProject.AddOption(homePage);
        fromProject.AddOption(license);
        fromProject.AddOption(screenshotUrls);
        fromProject.AddOption(summary);
        fromProject.AddOption(appId);
        fromProject.AddOption(executableName);
        fromProject.AddOption(isTerminal);
        fromProject.AddOption(iconOption);

        fromProject.SetHandler(async (FileInfo prj, string? ridVal, bool sc, string cfg, bool sf, bool tr, FileInfo outFile, Options opt) =>
        {
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
            var name = pub.Value.Name.GetValueOrDefault(null);
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
        }, project, rid, selfContained, configuration, singleFile, trimmed, output, optionsBinder);

        debCommand.AddCommand(fromProject);
    }

    private static void AddMsixSubcommands(Command msixCommand)
    {
        var inputDir = new Option<DirectoryInfo>("--directory", "The input directory (publish output)") { IsRequired = true };
        var outputFile = new Option<FileInfo>("--output", "Output .msix file") { IsRequired = true };
        var packCmd = new Command("pack", "Create an MSIX from a directory (expects AppxManifest.xml in the tree or pre-baked metadata). Experimental.");
        packCmd.AddOption(inputDir);
        packCmd.AddOption(outputFile);
        packCmd.SetHandler(async (DirectoryInfo inDir, FileInfo outFile) =>
        {
            var dirInfo = new System.IO.Abstractions.FileSystem().DirectoryInfo.New(inDir.FullName);
            var container = new Zafiro.DivineBytes.System.IO.DirectoryContainer(dirInfo);
            await DotnetPackaging.Msix.Msix.FromDirectory(container, Maybe<Serilog.ILogger>.None)
                .Bind(bytes => bytes.WriteTo(outFile.FullName))
                .WriteResult();
        }, inputDir, outputFile);
        msixCommand.AddCommand(packCmd);

        var project = new Option<FileInfo>("--project", "Path to the .csproj file") { IsRequired = true };
        var rid = new Option<string?>("--rid", "Runtime identifier (e.g. win-x64)");
        var selfContained = new Option<bool>("--self-contained", () => false, "Publish self-contained");
        var configuration = new Option<string>("--configuration", () => "Release", "Build configuration");
        var singleFile = new Option<bool>("--single-file", "Publish single-file");
        var trimmed = new Option<bool>("--trimmed", "Enable trimming");
        var outMsix = new Option<FileInfo>("--output", "Output .msix file") { IsRequired = true };
        var fromProject = new Command("from-project", "Publish a .NET project and build an MSIX from the published output (expects manifest/assets).");
        fromProject.AddOption(project);
        fromProject.AddOption(rid);
        fromProject.AddOption(selfContained);
        fromProject.AddOption(configuration);
        fromProject.AddOption(singleFile);
        fromProject.AddOption(trimmed);
        fromProject.AddOption(outMsix);
        fromProject.SetHandler(async (FileInfo prj, string? ridVal, bool sc, string cfg, bool sf, bool tr, FileInfo outFile) =>
        {
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
        }, project, rid, selfContained, configuration, singleFile, trimmed, outMsix);
        msixCommand.AddCommand(fromProject);
    }

    private static void AddAppImageFromProjectSubcommand(Command appImageCommand)
    {
        var project = new Option<FileInfo>("--project", "Path to the .csproj file") { IsRequired = true };
        var rid = new Option<string?>("--rid", "Runtime identifier (e.g. linux-x64, linux-arm64)");
        var selfContained = new Option<bool>("--self-contained", () => true, "Publish self-contained");
        var configuration = new Option<string>("--configuration", () => "Release", "Build configuration");
        var singleFile = new Option<bool>("--single-file", "Publish single-file");
        var trimmed = new Option<bool>("--trimmed", "Enable trimming");
        var output = new Option<FileInfo>("--output", "Output .AppImage file") { IsRequired = true };

        // Reuse metadata options via OptionsBinder
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
        var iconOption = new Option<IIcon?>("--icon", GetIcon ) { IsRequired = false, Description = "Path to the application icon" };

        var optionsBinder = new OptionsBinder(appName, startupWmClass, keywords, comment, mainCategory, additionalCategories, iconOption, version, homePage, license, screenshotUrls, summary, appId, executableName, isTerminal);

        var fromProject = new Command("from-project", "Publish a .NET project and build an AppImage from the published output.");
        fromProject.AddOption(project);
        fromProject.AddOption(rid);
        fromProject.AddOption(selfContained);
        fromProject.AddOption(configuration);
        fromProject.AddOption(singleFile);
        fromProject.AddOption(trimmed);
        fromProject.AddOption(output);
        fromProject.AddOption(appName);
        fromProject.AddOption(startupWmClass);
        fromProject.AddOption(mainCategory);
        fromProject.AddOption(additionalCategories);
        fromProject.AddOption(keywords);
        fromProject.AddOption(comment);
        fromProject.AddOption(version);
        fromProject.AddOption(homePage);
        fromProject.AddOption(license);
        fromProject.AddOption(screenshotUrls);
        fromProject.AddOption(summary);
        fromProject.AddOption(appId);
        fromProject.AddOption(executableName);
        fromProject.AddOption(isTerminal);
        fromProject.AddOption(iconOption);

        fromProject.SetHandler(async (FileInfo prj, string? ridVal, bool sc, string cfg, bool sf, bool tr, FileInfo outFile, Options opt) =>
        {
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
        }, project, rid, selfContained, configuration, singleFile, trimmed, output, optionsBinder);

        appImageCommand.AddCommand(fromProject);
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

    private static Task CreateDmg(DirectoryInfo inputDir, FileInfo outputFile, Options options)
    {
        var name = options.Name.GetValueOrDefault(inputDir.Name);
        return DotnetPackaging.Dmg.DmgIsoBuilder.Create(inputDir.FullName, outputFile.FullName, name);
    }

    private static void AddDmgFromProjectSubcommand(Command dmgCommand)
    {
        var project = new Option<FileInfo>("--project", "Path to the .csproj file") { IsRequired = true };
        var rid = new Option<string?>("--rid", "Runtime identifier (e.g. osx-x64, osx-arm64)") { IsRequired = true };
        var selfContained = new Option<bool>("--self-contained", () => true, "Publish self-contained");
        var configuration = new Option<string>("--configuration", () => "Release", "Build configuration");
        var singleFile = new Option<bool>("--single-file", "Publish single-file");
        var trimmed = new Option<bool>("--trimmed", "Enable trimming");
        var output = new Option<FileInfo>("--output", "Output .dmg file") { IsRequired = true };

        // Reuse metadata options to get volume name from --application-name if present
        var appName = new Option<string>("--application-name", "Application name / volume name") { IsRequired = false };
        var optionsBinder = new OptionsBinder(appName,
            new Option<string>("--wm-class"),
            new Option<IEnumerable<string>>("--keywords"),
            new Option<string>("--comment"),
            new Option<MainCategory?>("--main-category"),
            new Option<IEnumerable<AdditionalCategory>>("--additional-categories"),
            new Option<IIcon?>("--icon", GetIcon),
            new Option<string>("--version"),
            new Option<Uri>("--homepage"),
            new Option<string>("--license"),
            new Option<IEnumerable<Uri>>("--screenshot-urls"),
            new Option<string>("--summary"),
            new Option<string>("--appId"),
            new Option<string>("--executable-name"),
            new Option<bool>("--is-terminal"));

        var fromProject = new Command("from-project", "Publish a .NET project and build a .dmg from the published output (.app bundle auto-generated if missing). Experimental.");
        fromProject.AddOption(project);
        fromProject.AddOption(rid);
        fromProject.AddOption(selfContained);
        fromProject.AddOption(configuration);
        fromProject.AddOption(singleFile);
        fromProject.AddOption(trimmed);
        fromProject.AddOption(output);
        fromProject.AddOption(appName);

        fromProject.SetHandler(async (FileInfo prj, string? ridVal, bool sc, string cfg, bool sf, bool tr, FileInfo outFile, Options opt) =>
        {
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
        }, project, rid, selfContained, configuration, singleFile, trimmed, output, optionsBinder);

        dmgCommand.AddCommand(fromProject);
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
    private sealed class FlatpakOptionsBinder : System.CommandLine.Binding.BinderBase<FlatpakOptions>
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

        protected override FlatpakOptions GetBoundValue(System.CommandLine.Binding.BindingContext bindingContext)
        {
            var pr = bindingContext.ParseResult;
            var archStr = pr.GetValueForOption(arch);
            var parsedArch = string.IsNullOrWhiteSpace(archStr) ? null : ParseArchitecture(archStr!);
            return new FlatpakOptions
            {
                Runtime = pr.GetValueForOption(runtime)!,
                Sdk = pr.GetValueForOption(sdk)!,
                Branch = pr.GetValueForOption(branch)!,
                RuntimeVersion = pr.GetValueForOption(runtimeVersion)!,
                Shared = pr.GetValueForOption(shared)!,
                Sockets = pr.GetValueForOption(sockets)!,
                Devices = pr.GetValueForOption(devices)!,
                Filesystems = pr.GetValueForOption(filesystems)!,
                ArchitectureOverride = parsedArch == null ? Maybe<Architecture>.None : Maybe<Architecture>.From(parsedArch),
                CommandOverride = pr.GetValueForOption(command) is { } s && !string.IsNullOrWhiteSpace(s) ? Maybe<string>.From(s) : Maybe<string>.None
            };
        }
    }

    private sealed class ExeFromProjectExtra
    {
        public required FileInfo Output { get; init; }
        public FileInfo? Stub { get; init; }
        public string? Vendor { get; init; }
    }

    private sealed class ExeFromProjectExtraBinder : System.CommandLine.Binding.BinderBase<ExeFromProjectExtra>
    {
        private readonly Option<FileInfo> output;
        private readonly Option<FileInfo> stub;
        private readonly Option<string> vendor;

        public ExeFromProjectExtraBinder(Option<FileInfo> output, Option<FileInfo> stub, Option<string> vendor)
        {
            this.output = output;
            this.stub = stub;
            this.vendor = vendor;
        }

        protected override ExeFromProjectExtra GetBoundValue(System.CommandLine.Binding.BindingContext bindingContext)
        {
            var pr = bindingContext.ParseResult;
            return new ExeFromProjectExtra
            {
                Output = pr.GetValueForOption(output)!,
                Stub = pr.GetValueForOption(stub),
                Vendor = pr.GetValueForOption(vendor)
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
