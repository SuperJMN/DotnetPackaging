using System.CommandLine;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Rpm;
using Serilog;
using System.IO.Abstractions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;

namespace DotnetPackaging.Tool.Commands;
public static class RpmCommand
{
    public static Command GetCommand()
    {
        var command = CommandFactory.CreateCommand(
            "rpm",
            "RPM package",
            ".rpm",
            CreateRpm,
            "Create an RPM (.rpm) package suitable for Fedora, openSUSE, and other RPM-based distributions.",
            null,
            "pack-rpm");

        AddFromProjectSubcommand(command);
        return command;
    }

    private static Task CreateRpm(DirectoryInfo inputDir, FileInfo outputFile, Options options, ILogger logger)
    {
        logger.Debug("Packaging RPM artifact from {Directory}", inputDir.FullName);
        var fs = new FileSystem();
        var container = new DirectoryContainer(new DirectoryInfoWrapper(fs, inputDir)).AsRoot();

        var metadata = new FromDirectoryOptions();
        metadata.From(options);
        var packager = new RpmPackager();

        return packager.Pack(container, metadata, logger)
            .Bind(bytes => bytes.WriteTo(outputFile.FullName))
            .WriteResult();
    }

    private static void AddFromProjectSubcommand(Command rpmCommand)
    {
        var project = new Option<FileInfo>("--project") { Description = "Path to the .csproj file", Required = true };
        var arch = new Option<string?>("--arch") { Description = "Target architecture (x64, arm64). Auto-detects from current system if not specified." };
        var selfContained = new Option<bool>("--self-contained") { Description = "Publish self-contained [Deprecated]" };
        selfContained.DefaultValueFactory = _ => true;
        var configuration = new Option<string>("--configuration") { Description = "Build configuration" };
        configuration.DefaultValueFactory = _ => "Release";
        var singleFile = new Option<bool>("--single-file") { Description = "Publish single-file" };
        var trimmed = new Option<bool>("--trimmed") { Description = "Enable trimming" };
        var output = new Option<FileInfo>("--output") { Description = "Destination path for the generated .rpm", Required = true };

        var appName = new Option<string>("--application-name") { Description = "Application name", Required = false };
        appName.Aliases.Add("--productName");
        appName.Aliases.Add("--appName");
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
        iconOption.CustomParser = OptionsBinder.GetIcon;

        var optionsBinder = new OptionsBinder(appName, startupWmClass, keywords, comment, mainCategory, additionalCategories, iconOption, version, homePage, license, screenshotUrls, summary, appId, executableName, isTerminal);

        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build an RPM from the published output (no code duplication; library drives the pipeline)." };
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
            var logger = Log.ForContext("command", "rpm-from-project");

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

            var result = await new RpmPackager().PackProject(
                prj.FullName,
                outFile.FullName,
                o => o.From(opt),
                pub =>
                {
                    pub.SelfContained = true;
                    pub.Configuration = cfg;
                    pub.SingleFile = sf;
                    pub.Trimmed = tr;
                    var ridResult = RidUtils.ResolveLinuxRid(archVal, "rpm");
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

        rpmCommand.Add(fromProject);
    }
}
