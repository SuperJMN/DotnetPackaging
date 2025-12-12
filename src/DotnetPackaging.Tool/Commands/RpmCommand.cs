using System.CommandLine;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Rpm;
using Serilog;
using DotnetPackaging.Tool;
using System.IO.Abstractions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using System.Reactive.Disposables;

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

        return RpmFile.From()
            .Container(container)
            .Configure(configuration => configuration.From(options))
            .Build()
            .Bind(rpmFile => CopyRpmToOutput(rpmFile, outputFile))
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

    private static void AddFromProjectSubcommand(Command rpmCommand)
    {
        var project = new Option<FileInfo>("--project") { Description = "Path to the .csproj file", Required = true };
        var arch = new Option<string?>("--arch") { Description = "Target architecture (x64, arm64)" };
        var selfContained = new Option<bool>("--self-contained") { Description = "Publish self-contained" };
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

            await ExecutionWrapper.ExecuteWithPublishedProject(
                "rpm-from-project",
                outFile.FullName,
                prj,
                archVal,
                RidUtils.ResolveLinuxRid,
                sc, cfg, sf, tr,
                async (pub, l) =>
                {
                    var projectMetadata = ProjectMetadataReader.TryRead(prj, l);
                    var name = System.IO.Path.GetFileNameWithoutExtension(prj.Name);
                    var builder = RpmFile.From().Container(pub, name);
                    var rpmResult = await builder.Configure(o =>
                        {
                            o.From(opt);
                            if (projectMetadata.HasValue)
                            {
                                o.WithProjectMetadata(projectMetadata.Value);
                            }
                        }).Build();

                    if (rpmResult.IsFailure)
                    {
                        return rpmResult.ConvertFailure<IPackage>();
                    }

                    var rpmSource = ByteSource.FromStreamFactory(() => File.OpenRead(rpmResult.Value.FullName));
                    var resource = new Resource(outFile.Name, rpmSource);
                    var cleanup = Disposable.Create(() =>
                    {
                        if (File.Exists(rpmResult.Value.FullName))
                        {
                            File.Delete(rpmResult.Value.FullName);
                        }
                    });
                    var composite = new CompositeDisposable { pub, cleanup };
                    var package = (IPackage)new Package(resource.Name, resource, composite);
                    return Result.Success(package);
                });
        });

        rpmCommand.Add(fromProject);
    }
}
