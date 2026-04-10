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
        var metadata = new MetadataOptionSet();
        var binder = metadata.CreateBinder();

        // appimage appdir
        var inputDir = new Option<DirectoryInfo>("--directory") { Description = "The input directory (publish output)", Required = true };
        var outputDir = new Option<DirectoryInfo>("--output-dir") { Description = "Destination directory for the AppDir", Required = true };
        var appDirCmd = new Command("appdir") { Description = "Creates an AppDir from a directory (does not package an .AppImage). For .NET apps, pass the publish directory." };
        appDirCmd.Add(inputDir);
        appDirCmd.Add(outputDir);
        metadata.AddTo(appDirCmd);
        appDirCmd.SetAction(async parseResult =>
        {
            var directory = parseResult.GetValue(inputDir)!;
            var output = parseResult.GetValue(outputDir)!;
            var opts = binder.Bind(parseResult);
            await ExecutionWrapper.ExecuteWithLogging("appimage-appdir", output.FullName, logger => CreateAppDir(directory, output, opts, logger));
        });

        // appimage from-appdir
        var appDirPath = new Option<DirectoryInfo>("--directory") { Description = "The AppDir directory to package", Required = true };
        var outputFile = new Option<FileInfo>("--output") { Description = "Output .AppImage file", Required = true };
        var execRel = new Option<string>("--executable-relative-path") { Description = "Executable inside the AppDir (relative), e.g., usr/bin/MyApp", Required = false };
        var fromAppDirMetadata = new MetadataOptionSet();
        var fromAppDirBinder = fromAppDirMetadata.CreateBinder();
        var fromAppDirCmd = new Command("from-appdir") { Description = "Creates an AppImage from an existing AppDir directory." };
        fromAppDirCmd.Add(appDirPath);
        fromAppDirCmd.Add(outputFile);
        fromAppDirCmd.Add(execRel);
        fromAppDirMetadata.AddTo(fromAppDirCmd);
        fromAppDirCmd.SetAction(async parseResult =>
        {
            var directory = parseResult.GetValue(appDirPath)!;
            var output = parseResult.GetValue(outputFile)!;
            var relativeExec = parseResult.GetValue(execRel);
            var opts = fromAppDirBinder.Bind(parseResult);
            await ExecutionWrapper.ExecuteWithLogging("appimage-from-appdir", output.FullName, logger => CreateAppImageFromAppDir(directory, output, relativeExec, opts, logger));
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
        var metadata = new MetadataOptionSet();
        var project = new ProjectOptionSet(".AppImage");

        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build an AppImage from the published output." };
        project.AddTo(fromProject);
        metadata.AddTo(fromProject);

        var binder = metadata.CreateBinder();

        fromProject.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(project.Project)!;
            var cfg = parseResult.GetValue(project.Configuration)!;
            var sf = parseResult.GetValue(project.SingleFile);
            var tr = parseResult.GetValue(project.Trimmed);
            var outFile = parseResult.GetValue(project.Output)!;
            var opt = binder.Bind(parseResult);
            var archVal = parseResult.GetValue(project.Arch);
            var logger = Log.ForContext("command", "appimage-from-project");

            if (archVal == null)
            {
                archVal = ProjectOptionSet.AutoDetectArch(logger);
                if (archVal == null) return;
            }

            var result = await new AppImage.AppImagePackager().PackProject(
                prj.FullName,
                outFile.FullName,
                o => o.PackageOptions.From(opt),
                pub =>
                {
                    pub.SelfContained = true;
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
