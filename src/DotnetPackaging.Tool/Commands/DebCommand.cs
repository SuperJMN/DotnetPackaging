using System.CommandLine;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Deb;
using Serilog;
using DotnetPackaging.Tool;
using Zafiro.FileSystem.Core;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Tool.Commands;

public static class DebCommand
{
    public static Command GetCommand()
    {
        var command = CommandFactory.CreateCommand(
            "deb",
            "Debian package",
            ".deb",
            CreateDeb,
            "Create a Debian (.deb) installer for Debian and Ubuntu based distributions.",
            "pack-deb",
            "debian");
            
        AddFromProjectSubcommand(command);
        return command;
    }

    private static Task CreateDeb(DirectoryInfo inputDir, FileInfo outputFile, Options options, ILogger logger)
    {
        logger.Debug("Packaging Debian artifact from {Directory}", inputDir.FullName);
        var fs = new System.IO.Abstractions.FileSystem();
        return new Zafiro.FileSystem.Local.Directory(fs.DirectoryInfo.New(inputDir.FullName))
            .ToDirectory()
            .Bind(directory => DotnetPackaging.Deb.DebFile.From()
                .Directory(directory)
                .Configure(configuration => configuration.From(options))
                .Build()
                .Map(DebMixin.ToData)
                .Bind(async data =>
                {
                    await using var fileSystemStream = outputFile.Open(FileMode.Create);
                    return await data.DumpTo(fileSystemStream);
                }))
            .WriteResult();
    }

    private static void AddFromProjectSubcommand(Command debCommand)
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
        iconOption.CustomParser = OptionsBinder.GetIcon;

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
            var sc = parseResult.GetValue(selfContained);
            var cfg = parseResult.GetValue(configuration)!;
            var sf = parseResult.GetValue(singleFile);
            var tr = parseResult.GetValue(trimmed);
            var outFile = parseResult.GetValue(output)!;
            var opt = optionsBinder.Bind(parseResult);
            var ridVal = parseResult.GetValue(rid);

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
            var built = await DotnetPackaging.Deb.DebFile.From().Container(container, name).Configure(o => o.From(opt)).Build();
            if (built.IsFailure)
            {
                Console.Error.WriteLine(built.Error);
                Environment.ExitCode = 1;
                return;
            }

            var data = DebMixin.ToData(built.Value);
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
}
