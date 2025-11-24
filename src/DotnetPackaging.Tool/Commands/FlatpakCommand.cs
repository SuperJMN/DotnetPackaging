using System.CommandLine;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging.Flatpak;
using DotnetPackaging.Flatpak.Ostree;
using Serilog;
using Zafiro.DivineBytes.System.IO;
using DotnetPackaging.Tool;
using DotnetPackaging;
using Zafiro.DivineBytes;
using Zafiro.CSharpFunctionalExtensions;

namespace DotnetPackaging.Tool.Commands;

public static class FlatpakCommand
{
    private static readonly System.IO.Abstractions.FileSystem FileSystem = new();

    public static Command GetCommand()
    {
        var flatpakCommand = new Command("flatpak") { Description = "Flatpak packaging: generate layout, OSTree repo, or bundle (.flatpak). Can use system flatpak or internal bundler." };
        AddFlatpakSubcommands(flatpakCommand);
        return flatpakCommand;
    }

    private static void AddFlatpakSubcommands(Command flatpakCommand)
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
            await ExecutionWrapper.ExecuteWithLogging("flatpak-layout", output.FullName, logger => CreateFlatpakLayout(directory, output, opts, fpOpts, logger));
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
            await ExecutionWrapper.ExecuteWithLogging("flatpak-bundle", output.FullName, logger => CreateFlatpakBundle(directory, output, system, opts, fpOpts, logger));
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

            await ExecutionWrapper.ExecuteWithLogging("flatpak-from-project", outFile.FullName, async logger =>
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
                    Environment.ExitCode = 1;
                    return;
                }

                var archRes = await BuildUtils.GetArch(setup, execRes.Value);
                if (archRes.IsFailure)
                {
                    logger.Error("Architecture detection failed: {Error}", archRes.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var pm = await BuildUtils.CreateMetadata(setup, root, archRes.Value, execRes.Value, opt.IsTerminal.GetValueOrDefault(false), pub.Value.Name, logger);
                var planRes = await new FlatpakFactory().BuildPlan(root, pm, fopt);
                if (planRes.IsFailure)
                {
                    logger.Error("Flatpak plan generation failed: {Error}", planRes.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var plan = planRes.Value;

                if (!useSystemBundler)
                {
                    var internalBytes = FlatpakBundle.CreateOstree(plan);
                if (internalBytes.IsFailure)
                {
                    logger.Error("Internal bundler failed: {Error}", internalBytes.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var wr = await internalBytes.Value.WriteTo(outFile.FullName);
                if (wr.IsFailure)
                {
                    logger.Error("Failed writing bundle: {Error}", wr.Error);
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
                    Environment.ExitCode = 1;
                    return;
                }

                ProcessUtils.MakeExecutable(System.IO.Path.Combine(tmpAppDir, "files", "bin", plan.CommandName));
                ProcessUtils.MakeExecutable(System.IO.Path.Combine(tmpAppDir, "files", plan.ExecutableTargetPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));

                var effArch = fopt.ArchitectureOverride.GetValueOrDefault(plan.Metadata.Architecture).PackagePrefix;
                var cmd = fopt.CommandOverride.GetValueOrDefault(plan.CommandName);
                var finishArgs = $"build-finish \"{tmpAppDir}\" --command={cmd} {string.Join(" ", fopt.Shared.Select(s => $"--share={s}"))} {string.Join(" ", fopt.Sockets.Select(s => $"--socket={s}"))} {string.Join(" ", fopt.Devices.Select(d => $"--device={d}"))} {string.Join(" ", fopt.Filesystems.Select(f => $"--filesystem={f}"))}";
                var finish = ProcessUtils.Run("flatpak", finishArgs);
                if (finish.IsFailure)
                {
                    logger.Error("flatpak build-finish failed: {Error}", finish.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var export = ProcessUtils.Run("flatpak", $"build-export --arch={effArch} \"{tmpRepoDir}\" \"{tmpAppDir}\" {fopt.Branch}");
                if (export.IsFailure)
                {
                    logger.Error("flatpak build-export failed: {Error}", export.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var bundle = ProcessUtils.Run("flatpak", $"build-bundle \"{tmpRepoDir}\" \"{outFile.FullName}\" {plan.AppId} {fopt.Branch} --arch={effArch}");
                if (bundle.IsFailure)
                {
                    logger.Error("flatpak build-bundle failed: {Error}", bundle.Error);
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
            await ExecutionWrapper.ExecuteWithLogging("flatpak-repo", output.FullName, logger => CreateFlatpakRepo(directory, output, opts, fpOpts, logger));
        });

        flatpakCommand.Add(repoCmd);

        // flatpak pack
        var packInputDir = new Option<DirectoryInfo>("--directory") { Description = "The input directory (publish output)", Required = true };
        var packOutputDir = new Option<DirectoryInfo>("--output-dir") { Description = "Destination directory for the resulting .flatpak", Required = true };
        var packCmd = new Command("pack") { Description = "Pack a .flatpak with minimal parameters. Uses system flatpak if available, otherwise falls back to internal bundler." };
        packCmd.Add(packInputDir);
        packCmd.Add(packOutputDir);
        packCmd.SetAction(async parseResult =>
        {
            var inDir = parseResult.GetValue(packInputDir)!;
            var outDir = parseResult.GetValue(packOutputDir)!;
            await ExecutionWrapper.ExecuteWithLogging("flatpak-pack", outDir.FullName, async logger =>
            {
                var dirInfo = FileSystem.DirectoryInfo.New(inDir.FullName);
                var container = new DirectoryContainer(dirInfo);
                var root = container.AsRoot();
                var setup = new FromDirectoryOptions();

                var execRes = await BuildUtils.GetExecutable(root, setup, logger);
                if (execRes.IsFailure) { logger.Error("Executable discovery failed: {Error}", execRes.Error); Environment.ExitCode = 1; return; }
                var archRes = await BuildUtils.GetArch(setup, execRes.Value);
                if (archRes.IsFailure) { logger.Error("Architecture detection failed: {Error}", archRes.Error); Environment.ExitCode = 1; return; }
                var pm = await BuildUtils.CreateMetadata(setup, root, archRes.Value, execRes.Value, setup.IsTerminal, Maybe<string>.From(inDir.Name), logger);
                var planRes = await new FlatpakFactory().BuildPlan(root, pm, new FlatpakOptions());
                if (planRes.IsFailure) { logger.Error("Flatpak plan generation failed: {Error}", planRes.Error); Environment.ExitCode = 1; return; }
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
                    ProcessUtils.MakeExecutable(System.IO.Path.Combine(tmpAppDir, "files", "bin", plan.CommandName));
                    ProcessUtils.MakeExecutable(System.IO.Path.Combine(tmpAppDir, "files", plan.ExecutableTargetPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
                    var arch = plan.Metadata.Architecture.PackagePrefix;
                    var finish = ProcessUtils.Run("flatpak", $"build-finish \"{tmpAppDir}\" --command={plan.CommandName} --socket=wayland --socket=x11 --socket=pulseaudio --share=network --share=ipc --device=dri --filesystem=home");
                    if (finish.IsSuccess)
                    {
                        var export = ProcessUtils.Run("flatpak", $"build-export --arch={arch} \"{tmpRepoDir}\" \"{tmpAppDir}\" stable");
                        if (export.IsSuccess)
                        {
                            var bundle = ProcessUtils.Run("flatpak", $"build-bundle \"{tmpRepoDir}\" \"{outPath}\" {plan.AppId} stable --arch={arch}");
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
                    return;
                }
                var writeRes = await internalBytes.Value.WriteTo(outPath);
                if (writeRes.IsFailure)
                {
                    logger.Error("Failed writing bundle: {Error}", writeRes.Error);
                }
                else
                {
                    logger.Information("{OutputFile}", outPath);
                }
            });
        });
        
        flatpakCommand.Add(packCmd);
    }
    
    private static Task CreateFlatpakLayout(DirectoryInfo inputDir, DirectoryInfo outputDir, Options options, FlatpakOptions flatpakOptions, ILogger logger)
    {
        logger.Debug("Generating Flatpak layout from {Directory}", inputDir.FullName);
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

                    var wrapperPath = System.IO.Path.Combine(tmpAppDir, "files", "bin", p.CommandName);
                    var exePath = System.IO.Path.Combine(tmpAppDir, "files", p.ExecutableTargetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                    ProcessUtils.MakeExecutable(wrapperPath);
                    ProcessUtils.MakeExecutable(exePath);

                    var effArch = flatpakOptions.ArchitectureOverride.GetValueOrDefault(p.Metadata.Architecture).PackagePrefix;
                    var appId = p.AppId;
                    var cmd = flatpakOptions.CommandOverride.GetValueOrDefault(p.CommandName);
                    var finishArgs = $"build-finish \"{tmpAppDir}\" --command={cmd} {string.Join(" ", flatpakOptions.Shared.Select(s => $"--share={s}"))} {string.Join(" ", flatpakOptions.Sockets.Select(s => $"--socket={s}"))} {string.Join(" ", flatpakOptions.Devices.Select(d => $"--device={d}"))} {string.Join(" ", flatpakOptions.Filesystems.Select(f => $"--filesystem={f}"))}";
                    var finish = ProcessUtils.Run("flatpak", finishArgs);
                    if (finish.IsFailure) return Result.Failure(finish.Error);
                    var export = ProcessUtils.Run("flatpak", $"build-export --arch={effArch} \"{tmpRepoDir}\" \"{tmpAppDir}\" {flatpakOptions.Branch}");
                    if (export.IsFailure) return Result.Failure(export.Error);
                    var bundle = ProcessUtils.Run("flatpak", $"build-bundle \"{tmpRepoDir}\" \"{outputFile.FullName}\" {appId} {flatpakOptions.Branch} --arch={effArch}");
                    if (bundle.IsFailure) return Result.Failure(bundle.Error);
                    return Result.Success();
                });
            })
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
            .Bind(plan => OstreeRepoBuilder.Build(plan))
            .Bind(repo => repo.WriteTo(outputDir.FullName))
            .WriteResult();
    }
}
