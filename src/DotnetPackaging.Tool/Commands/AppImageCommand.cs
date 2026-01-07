using System.CommandLine;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Metadata;
using DotnetPackaging;
using Serilog;
using Zafiro.DivineBytes.System.IO;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Tool.Commands;

public static class AppImageCommand
{
    private static readonly System.IO.Abstractions.FileSystem FileSystem = new();

    public static Command GetCommand()
    {
        var command = CommandFactory.CreateCommand(
            "appimage",
            "AppImage package",
            ".AppImage",
            CreateAppImage,
            "Create a portable AppImage (.AppImage) from a published directory. Use subcommands for AppDir workflows.",
            null,
            "pack-appimage");

        AddAppImageSubcommands(command);
        AddFromProjectSubcommand(command);
        return command;
    }

    private static Task CreateAppImage(DirectoryInfo inputDir, FileInfo outputFile, Options options, ILogger logger)
    {
        logger.Debug("Packaging AppImage from {Directory}", inputDir.FullName);
        var dirInfo = FileSystem.DirectoryInfo.New(inputDir.FullName);
        var container = new DirectoryContainer(dirInfo);
        var root = container.AsRoot();

        var metadata = new AppImagePackagerMetadata();
        metadata.PackageOptions.From(options);
        var packager = new AppImagePackager();

        return packager.Pack(root, metadata, logger)
            .Bind(source => source.WriteTo(outputFile.FullName))
            .WriteResult();
    }

    private static Task<Result<AppImageMetadata>> BuildAppImageMetadata(Options options, IContainer applicationRoot, Maybe<ProjectMetadata> projectMetadata, ILogger logger)
    {
        var setup = new FromDirectoryOptions();
        setup.From(options);
        if (projectMetadata.HasValue)
        {
            setup.WithProjectMetadata(projectMetadata.Value);
        }

        return BuildUtils.GetExecutable(applicationRoot, setup, logger)
            .Bind(exec =>
            {
                var appName = projectMetadata.HasValue
                    ? ApplicationNameResolver.FromProject(options.Name, projectMetadata, exec.Name)
                    : ApplicationNameResolver.FromDirectory(options.Name, exec.Name);
                var packageName = appName.ToLowerInvariant().Replace(" ", "").Replace("-", "");
                var appId = options.Id.GetValueOrDefault($"com.{packageName}");

                var metadata = new AppImageMetadata(appId, appName, packageName)
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

                return Result.Success(metadata);
            });
    }

    private static Maybe<IEnumerable<string>> BuildCategories(Options options)
    {
        var list = new List<string>();
        if (options.MainCategory.HasValue) list.Add(options.MainCategory.Value.ToString());
        if (options.AdditionalCategories.HasValue) list.AddRange(options.AdditionalCategories.Value.Select(x => x.ToString()));
        return list.Any() ? list : Maybe<IEnumerable<string>>.None;
    }

    private static void AddAppImageSubcommands(Command appImageCommand)
    {
        var appName = new Option<string>("--application-name") { Description = "Application name", Required = false };
        appName.Aliases.Add("--productName");
        appName.Aliases.Add("--appName");
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
        iconOption.CustomParser = OptionsBinder.GetIcon;

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
            await ExecutionWrapper.ExecuteWithLogging("appimage-appdir", output.FullName, logger => CreateAppDir(directory, output, metadata, logger));
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
            await ExecutionWrapper.ExecuteWithLogging("appimage-from-appdir", output.FullName, logger => CreateAppImageFromAppDir(directory, output, relativeExec, metadata, logger));
        });

        appImageCommand.Add(appDirCmd);
        appImageCommand.Add(fromAppDirCmd);
    }

    private static Task CreateAppDir(DirectoryInfo inputDir, DirectoryInfo outputDir, Options options, ILogger logger)
    {
        logger.Debug("Building AppDir from {Directory}", inputDir.FullName);
        var dirInfo = FileSystem.DirectoryInfo.New(inputDir.FullName);
        var container = new DirectoryContainer(dirInfo);
        var root = container.AsRoot();

        return BuildAppImageMetadata(options, root, Maybe<ProjectMetadata>.None, logger)
            .Bind(metadata => new AppImageFactory().BuildAppDir(root, metadata))
            .Bind(rootDir => rootDir.WriteTo(outputDir.FullName))
            .WriteResult();
    }

    private static Task CreateAppImageFromAppDir(DirectoryInfo appDir, FileInfo outputFile, string? executableRelativePath, Options options, ILogger logger)
    {
        logger.Debug("Packaging AppImage from AppDir {Directory}", appDir.FullName);
        var dirInfo = FileSystem.DirectoryInfo.New(appDir.FullName);
        var container = new DirectoryContainer(dirInfo);
        var root = container.AsRoot();

        return BuildAppImageMetadata(options, root, Maybe<ProjectMetadata>.None, logger)
            .Bind(metadata => new AppImageFactory().CreateFromAppDir(root, metadata, executableRelativePath, null))
            .Bind(x => x.ToByteSource())
            .Bind(source => source.WriteTo(outputFile.FullName))
            .WriteResult();
    }

    private static void AddFromProjectSubcommand(Command appImageCommand)
    {
        var project = new Option<FileInfo>("--project") { Description = "Path to the .csproj file", Required = true };
        var arch = new Option<string?>("--arch") { Description = "Target architecture (x64, arm64). Auto-detects from current system if not specified." };
        var selfContained = new Option<bool>("--self-contained") { Description = "Publish self-contained" };
        selfContained.DefaultValueFactory = _ => true;
        var configuration = new Option<string>("--configuration") { Description = "Build configuration" };
        configuration.DefaultValueFactory = _ => "Release";
        var singleFile = new Option<bool>("--single-file") { Description = "Publish single-file" };
        var trimmed = new Option<bool>("--trimmed") { Description = "Enable trimming" };
        var output = new Option<FileInfo>("--output") { Description = "Output .AppImage file", Required = true };

        var appName = new Option<string>("--application-name") { Description = "Application name", Required = false };
        appName.Aliases.Add("--productName");
        appName.Aliases.Add("--appName");
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
        iconOption.CustomParser = OptionsBinder.GetIcon;

        var optionsBinder = new OptionsBinder(appName, startupWmClass, keywords, comment, mainCategory, additionalCategories, iconOption, version, homePage, license, screenshotUrls, summary, appId, executableName, isTerminal);

        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build an AppImage from the published output." };
        fromProject.Add(project);
        fromProject.Add(arch);
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
            var sc = parseResult.GetValue(selfContained);
            var cfg = parseResult.GetValue(configuration)!;
            var sf = parseResult.GetValue(singleFile);
            var tr = parseResult.GetValue(trimmed);
            var outFile = parseResult.GetValue(output)!;
            var opt = optionsBinder.Bind(parseResult);
            var archVal = parseResult.GetValue(arch);
            var logger = Log.ForContext("command", "appimage-from-project");

            // Auto-detect architecture if not specified
            if (archVal == null)
            {
                archVal = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
                {
                    System.Runtime.InteropServices.Architecture.X64 => "x64",
                    System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
                    System.Runtime.InteropServices.Architecture.Arm => "arm",
                    System.Runtime.InteropServices.Architecture.X86 => "x86",
                    _ => null
                };

                if (archVal != null)
                {
                    logger.Information("Architecture not specified, auto-detected: {Arch}", archVal);
                }
                else
                {
                    logger.Error("Unable to auto-detect architecture. Please specify --arch explicitly (e.g., --arch x64)");
                    return;
                }
            }

            var result = await new AppImage.AppImagePackager().PackProject(
                prj.FullName,
                outFile.FullName,
                o => o.PackageOptions.From(opt),
                pub =>
                {
                    pub.SelfContained = sc;
                    pub.Configuration = cfg;
                    pub.SingleFile = sf;
                    pub.Trimmed = tr;
                    var ridResult = RidUtils.ResolveLinuxRid(archVal, "appimage");
                    if (ridResult.IsSuccess)
                    {
                        pub.Rid = ridResult.Value;
                        logger.Debug("Using RID: {Rid}", pub.Rid);
                    }
                    else
                    {
                        logger.Error("Failed to resolve RID for architecture {Arch}: {Error}", archVal, ridResult.Error);
                    }
                },
                logger);

            result.WriteResult();
        });

        appImageCommand.Add(fromProject);
    }
}
