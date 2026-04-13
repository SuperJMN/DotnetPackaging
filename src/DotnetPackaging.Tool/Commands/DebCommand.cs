using System.CommandLine;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Deb;
using Serilog;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using System.IO.Abstractions;

namespace DotnetPackaging.Tool.Commands;

public static class DebCommand
{
    public static Command GetCommand()
    {
        var serviceOptions = new ServiceOptionSet();

        var commands = CommandFactory.CreateCommand(
            "deb",
            "Debian package",
            ".deb",
            CreateDeb,
            "Create a Debian (.deb) installer for Debian and Ubuntu based distributions.",
            null,
            serviceOptions.Apply,
            "pack-deb",
            "debian");

        serviceOptions.AddTo(commands.Root);
        serviceOptions.AddTo(commands.FromDirectory);

        AddFromProjectSubcommand(commands.Root, serviceOptions);
        return commands.Root;
    }

    private static Task CreateDeb(DirectoryInfo inputDir, FileInfo outputFile, Options options, ILogger logger)
    {
        logger.Debug("Packaging Debian artifact from {Directory}", inputDir.FullName);
        var fs = new FileSystem();
        var container = new DirectoryContainer(new DirectoryInfoWrapper(fs, inputDir)).AsRoot();

        var metadata = new FromDirectoryOptions();
        metadata.From(options);
        var packager = new Deb.DebPackager();

        return packager.Pack(container, metadata, logger)
            .Bind(bytes => bytes.WriteTo(outputFile.FullName))
            .WriteResult();
    }

    private static void AddFromProjectSubcommand(Command debCommand, ServiceOptionSet serviceOptions)
    {
        var metadata = new MetadataOptionSet();
        var project = new ProjectOptionSet(".deb");

        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build a Debian .deb from the published output." };
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
            var logger = Log.ForContext("command", "deb-from-project");

            serviceOptions.Apply(opt, parseResult);

            if (archVal == null)
            {
                archVal = ProjectOptionSet.AutoDetectArch(logger);
                if (archVal == null) return;
            }

            var result = await new Deb.DebPackager().PackProject(
                prj.FullName,
                outFile.FullName,
                o => o.From(opt),
                pub =>
                {
                    pub.SelfContained = true;
                    pub.Configuration = cfg;
                    pub.SingleFile = sf;
                    pub.Trimmed = tr;
                    var ridResult = RidUtils.ResolveLinuxRid(archVal, "deb");
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

        debCommand.Add(fromProject);
    }

}
