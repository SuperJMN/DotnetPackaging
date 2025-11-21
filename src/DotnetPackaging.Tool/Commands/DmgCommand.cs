using System.CommandLine;
using CSharpFunctionalExtensions;
using DotnetPackaging.Dmg;
using Serilog;

namespace DotnetPackaging.Tool.Commands;

public static class DmgCommand
{
    private static readonly System.IO.Abstractions.FileSystem FileSystem = new();

    public static Command GetCommand()
    {
        // DMG command (experimental, cross-platform)
        var dmgCommand = CommandFactory.CreateCommand(
            "dmg",
            "macOS disk image",
            ".dmg",
            CreateDmg,
            "Create a simple macOS disk image (.dmg). Currently uses an ISO/UDF (UDTO) payload for broad compatibility.",
            "pack-dmg");
        
        AddDmgFromProjectSubcommand(dmgCommand);
        
        // dmg verify subcommand
        var verifyFileOption = new Option<FileInfo>("--file")
        {
            Description = "Path to the .dmg file",
            Required = true
        };
        var verifyCmd = new Command("verify", "Verify that a .dmg file has a macOS-friendly structure (ISO/UDTO or UDIF).")
        {
            verifyFileOption
        };
        verifyCmd.SetAction(async parseResult =>
        {
            var file = parseResult.GetValue(verifyFileOption)!;
            await ExecutionWrapper.ExecuteWithLogging("dmg-verify", file.FullName, async logger =>
            {
                var result = await DmgVerifier.Verify(file.FullName);
                if (result.IsFailure)
                {
                    logger.Error("Verification failed: {Error}", result.Error);
                    Environment.ExitCode = 1;
                }
                else
                {
                    logger.Information("{VerificationResult}", result.Value);
                }
            });
        });
        dmgCommand.Add(verifyCmd);

        return dmgCommand;
    }

    private static Task CreateDmg(DirectoryInfo inputDir, FileInfo outputFile, Options options, ILogger logger)
    {
        logger.Debug("Packaging DMG artifact from {Directory}", inputDir.FullName);
        var name = options.Name.GetValueOrDefault(inputDir.Name);
        return DmgIsoBuilder.Create(inputDir.FullName, outputFile.FullName, name);
    }

    private static void AddDmgFromProjectSubcommand(Command dmgCommand)
    {
        var project = new Option<FileInfo>("--project") { Description = "Path to the .csproj file", Required = true };
        var arch = new Option<string?>("--arch") { Description = "Target architecture (x64, arm64)" };
        var selfContained = new Option<bool>("--self-contained") { Description = "Publish self-contained" };
        selfContained.DefaultValueFactory = _ => true;
        var configuration = new Option<string>("--configuration") { Description = "Build configuration" };
        configuration.DefaultValueFactory = _ => "Release";
        var singleFile = new Option<bool>("--single-file") { Description = "Publish single-file" };
        var trimmed = new Option<bool>("--trimmed") { Description = "Enable trimming" };
        var output = new Option<FileInfo>("--output") { Description = "Output .dmg file", Required = true };

        // Reuse metadata options to get volume name from --application-name if present
        var appName = new Option<string>("--application-name") { Description = "Application name / volume name", Required = false };
        var dmgIconOption = new Option<IIcon?>("--icon") { Description = "Path to the application icon" };
        dmgIconOption.CustomParser = OptionsBinder.GetIcon;

        var optionsBinder = new OptionsBinder(appName,
            new Option<string>("--wm-class"),
            new Option<IEnumerable<string>>("--keywords"),
            new Option<string>("--comment"),
            new Option<MainCategory?>("--main-category"),
            new Option<IEnumerable<AdditionalCategory>>("--additional-categories"),
            dmgIconOption,
            new Option<string>("--version"),
            new Option<Uri>("--homepage"),
            new Option<string>("--license"),
            new Option<IEnumerable<Uri>>("--screenshot-urls"),
            new Option<string>("--summary"),
            new Option<string>("--appId"),
            new Option<string>("--executable-name"),
            new Option<bool>("--is-terminal"));

        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build a .dmg from the published output (.app bundle auto-generated if missing). Experimental." };
        fromProject.Add(project);
        fromProject.Add(arch);
        fromProject.Add(selfContained);
        fromProject.Add(configuration);
        fromProject.Add(singleFile);
        fromProject.Add(trimmed);
        fromProject.Add(output);
        fromProject.Add(appName);

        fromProject.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(project)!;
            var sc = parseResult.GetValue(selfContained);
            var cfg = parseResult.GetValue(configuration)!;
            var sf = parseResult.GetValue(singleFile);
            var tr = parseResult.GetValue(trimmed);
            var outFile = parseResult.GetValue(output)!;
            var opt = optionsBinder.Bind(parseResult);
            var ridVal = parseResult.GetValue(arch);

            await ExecutionWrapper.ExecuteWithLogging("dmg-from-project", outFile.FullName, async logger =>
            {
                var ridResult = RidUtils.ResolveMacRid(ridVal);
                if (ridResult.IsFailure)
                {
                    logger.Error("Invalid RID: {Error}", ridResult.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var publisher = new DotnetPackaging.Publish.DotnetPublisher();
                var req = new DotnetPackaging.Publish.ProjectPublishRequest(prj.FullName)
                {
                    Rid = Maybe<string>.From(ridResult.Value),
                    SelfContained = sc,
                    Configuration = cfg,
                    SingleFile = sf,
                    Trimmed = tr
                };

                var pub = await publisher.Publish(req);
                if (pub.IsFailure)
                {
                    logger.Error("Publish failed: {Error}", pub.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var volName = opt.Name.GetValueOrDefault(pub.Value.Name.GetValueOrDefault("App"));
                await DmgIsoBuilder.Create(pub.Value.OutputDirectory, outFile.FullName, volName);
                logger.Information("Success");
            });
        });

        dmgCommand.Add(fromProject);
    }
}
