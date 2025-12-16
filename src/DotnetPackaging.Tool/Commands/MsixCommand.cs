using System.CommandLine;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix;
using Serilog;
using Zafiro.DivineBytes.System.IO;
using DotnetPackaging.Tool;
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
        var packCmd = new Command("pack") { Description = "Create an MSIX from a directory (expects AppxManifest.xml in the tree or pre-baked metadata). Experimental." };
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
                var result = await DotnetPackaging.Msix.Msix.FromDirectory(container, Maybe<Serilog.ILogger>.From(logger))
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

        var project = new Option<FileInfo>("--project") { Description = "Path to the .csproj file", Required = true };
        var arch = new Option<string?>("--arch") { Description = "Target architecture (x64, arm64)" };
        var selfContained = new Option<bool>("--self-contained") { Description = "Publish self-contained" };
        selfContained.DefaultValueFactory = _ => false;
        var configuration = new Option<string>("--configuration") { Description = "Build configuration" };
        configuration.DefaultValueFactory = _ => "Release";
        var singleFile = new Option<bool>("--single-file") { Description = "Publish single-file" };
        var trimmed = new Option<bool>("--trimmed") { Description = "Enable trimming" };
        var outMsix = new Option<FileInfo>("--output") { Description = "Output .msix file", Required = true };
        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build an MSIX from the published output (expects manifest/assets)." };
        fromProject.Add(project);
        fromProject.Add(arch);
        fromProject.Add(selfContained);
        fromProject.Add(configuration);
        fromProject.Add(singleFile);
        fromProject.Add(trimmed);
        fromProject.Add(outMsix);
        fromProject.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(project)!;
            var sc = parseResult.GetValue(selfContained);
            var cfg = parseResult.GetValue(configuration)!;
            var sf = parseResult.GetValue(singleFile);
            var tr = parseResult.GetValue(trimmed);
            var outFile = parseResult.GetValue(outMsix)!;
            var archVal = parseResult.GetValue(arch);
            var logger = Log.ForContext("command", "msix-from-project");

            var result = await Msix.MsixProjectPackager.PackProject(
                prj.FullName,
                outFile.FullName,
                pub =>
                {
                    pub.SelfContained = sc;
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
