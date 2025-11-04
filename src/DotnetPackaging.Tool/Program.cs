using System.CommandLine;
using System.CommandLine.Parsing;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage;
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

namespace DotnetPackaging.Tool;

static class Program
{
    private static readonly System.IO.Abstractions.FileSystem FileSystem = new();
    
    public static Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {Platform}] {Message:lj}{NewLine}{Exception}")
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
        rootCommand.AddCommand(debCommand);

        var rpmCommand = CreateCommand(
            "rpm",
            "RPM package",
            ".rpm",
            CreateRpm,
            "Create an RPM (.rpm) package suitable for Fedora, openSUSE, and other RPM-based distributions.",
            "pack-rpm");
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
        rootCommand.AddCommand(appImageCommand);

        // DMG command (experimental, cross-platform)
        var dmgCommand = CreateCommand(
            "dmg",
            "macOS disk image",
            ".dmg",
            CreateDmg,
            "Create a simple macOS disk image (.dmg). Currently uses an ISO/UDF (UDTO) payload for broad compatibility.",
            "pack-dmg");
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
                Console.WriteLine(result.Value);
            }
        }, verifyCmd.Options.OfType<Option<FileInfo>>().First());
        dmgCommand.AddCommand(verifyCmd);

        // Flatpak command
        var flatpakCommand = new Command("flatpak", "Flatpak packaging: generate layout, OSTree repo, or bundle (.flatpak). Can use system flatpak or internal bundler.");
        AddFlatpakSubcommands(flatpakCommand);
        rootCommand.AddCommand(flatpakCommand);
        
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
                            Console.WriteLine(outPath);
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
                Console.WriteLine(outPath);
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
