using System.CommandLine;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix;
using DotnetPackaging.Msix.Core.Manifest;
using Serilog;
using Zafiro.DivineBytes.System.IO;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Tool.Commands;

public static class MsixCommand
{
    public static Command GetCommand()
    {
        var msixCommand = new Command("msix") { Description = "MSIX packaging (experimental)" };
        AddMsixSubcommands(msixCommand);
        return msixCommand;
    }

    private static void AddMsixSubcommands(Command msixCommand)
    {
        var inputDir = new Option<DirectoryInfo>("--directory") { Description = "The input directory (publish output)", Required = true };
        var outputFile = new Option<FileInfo>("--output") { Description = "Output .msix file", Required = true };
        var packCmd = new Command("from-directory") { Description = "Create an MSIX from a directory (expects AppxManifest.xml in the tree or pre-baked metadata). Experimental." };
        packCmd.Aliases.Add("pack");
        packCmd.Add(inputDir);
        packCmd.Add(outputFile);
        packCmd.SetAction(async parseResult =>
        {
            var inDir = parseResult.GetValue(inputDir)!;
            var outFile = parseResult.GetValue(outputFile)!;
            await ExecutionWrapper.ExecuteWithLogging("msix-pack", outFile.FullName, async logger =>
            {
                var dirInfo = new System.IO.Abstractions.FileSystem().DirectoryInfo.New(inDir.FullName);
                var container = new DirectoryContainer(dirInfo);
                var packager = new MsixPackager();
                var result = await packager.Pack(container, Maybe<AppManifestMetadata>.None, logger)
                    .Bind(bytes => bytes.WriteTo(outFile.FullName));

                if (result.IsFailure)
                {
                    logger.Error("MSIX packaging failed: {Error}", result.Error);
                    Environment.ExitCode = 1;
                }
                else
                {
                    logger.Information("Success");
                }
            });
        });
        msixCommand.Add(packCmd);

        var project = new ProjectOptionSet(".msix");
        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build an MSIX from the published output (expects manifest/assets)." };
        project.AddTo(fromProject);
        fromProject.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(project.Project)!;
            var cfg = parseResult.GetValue(project.Configuration)!;
            var sf = parseResult.GetValue(project.SingleFile);
            var tr = parseResult.GetValue(project.Trimmed);
            var outFile = parseResult.GetValue(project.Output)!;
            var archVal = parseResult.GetValue(project.Arch);
            var logger = Log.ForContext("command", "msix-from-project");

            var result = await new MsixPackager().PackProject(
                prj.FullName,
                outFile.FullName,
                null,
                pub =>
                {
                    pub.SelfContained = true;
                    pub.Configuration = cfg;
                    pub.SingleFile = sf;
                    pub.Trimmed = tr;
                    if (archVal != null)
                    {
                        var ridResult = RidUtils.ResolveWindowsRid(archVal, "msix");
                        if (ridResult.IsSuccess) pub.Rid = ridResult.Value;
                    }
                },
                logger);

            result.WriteResult();
        });
        msixCommand.Add(fromProject);
    }
}
