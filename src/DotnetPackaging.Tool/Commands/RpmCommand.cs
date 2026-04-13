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
        var serviceOptions = new ServiceOptionSet();

        var commands = CommandFactory.CreateCommand(
            "rpm",
            "RPM package",
            ".rpm",
            CreateRpm,
            "Create an RPM (.rpm) package suitable for Fedora, openSUSE, and other RPM-based distributions.",
            null,
            serviceOptions.Apply,
            "pack-rpm");

        serviceOptions.AddTo(commands.Root);
        serviceOptions.AddTo(commands.FromDirectory);

        AddFromProjectSubcommand(commands.Root, serviceOptions);
        return commands.Root;
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

    private static void AddFromProjectSubcommand(Command rpmCommand, ServiceOptionSet serviceOptions)
    {
        var metadata = new MetadataOptionSet();
        var project = new ProjectOptionSet(".rpm");

        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build an RPM from the published output (no code duplication; library drives the pipeline)." };
        project.AddTo(fromProject);
        metadata.AddTo(fromProject);
        serviceOptions.AddTo(fromProject);

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
            var logger = Log.ForContext("command", "rpm-from-project");

            serviceOptions.Apply(opt, parseResult);

            if (archVal == null)
            {
                archVal = ProjectOptionSet.AutoDetectArch(logger);
                if (archVal == null) return;
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
