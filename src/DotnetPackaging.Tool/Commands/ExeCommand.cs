using System.CommandLine;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Tool.Commands;

public static class ExeCommand
{
    public static Command GetCommand()
    {
        var exeCommand = new Command("exe") { Description = "Windows self-extracting installer (.exe). If --stub is not provided, the tool downloads the appropriate stub from GitHub Releases." };
        var exeInputDir = new Option<DirectoryInfo>("--directory")
        {
            Description = "The input directory (publish output)",
            Required = true
        };
        var exeOutput = new Option<FileInfo>("--output")
        {
            Description = "Output installer .exe",
            Required = true
        };
        var stubPath = new Option<FileInfo>("--stub")
        {
            Description = "Path to the prebuilt stub (WinExe) to concatenate (optional if repo layout is present)"
        };
        var setupLogo = new Option<FileInfo?>("--setup-logo")
        {
            Description = "Path to a logo image displayed by the installer and uninstaller wizards"
        };
        var exArchTop = new Option<string?>("--arch")
        {
            Description = "Target architecture for the stub (x64, arm64)"
        };

        // Reuse metadata options
        var exAppName = new Option<string>("--application-name")
        {
            Description = "Application name",
            Required = false
        };
        var exComment = new Option<string>("--comment")
        {
            Description = "Comment / long description",
            Required = false
        };
        var exVersion = new Option<string>("--version")
        {
            Description = "Version",
            Required = false
        };
        var exAppId = new Option<string>("--appId")
        {
            Description = "Application Id (Reverse DNS typical)",
            Required = false
        };
        var exVendor = new Option<string>("--vendor")
        {
            Description = "Vendor/Publisher",
            Required = false
        };
        var exExecutableName = new Option<string>("--executable-name")
        {
            Description = "Name of your application's executable",
            Required = false
        };
        var exIconOption = new Option<IIcon?>("--icon")
        {
            Description = "Path to the application icon"
        };
        exIconOption.CustomParser = OptionsBinder.GetIcon;

        var optionsBinder = new OptionsBinder(
            exAppName,
            new Option<string>("--wm-class"),
            new Option<IEnumerable<string>>("--keywords"),
            exComment,
            new Option<MainCategory?>("--main-category"),
            new Option<IEnumerable<AdditionalCategory>>("--additional-categories"),
            exIconOption,
            exVersion,
            new Option<Uri>("--homepage"),
            new Option<string>("--license"),
            new Option<IEnumerable<Uri>>("--screenshot-urls"),
            new Option<string>("--summary"),
            exAppId,
            exExecutableName,
            new Option<bool>("--is-terminal")
        );

        exeCommand.Add(exeInputDir);
        exeCommand.Add(exeOutput);
        exeCommand.Add(stubPath);
        exeCommand.Add(setupLogo);
        // Make metadata options global so subcommands can use them without re-adding
        exAppName.Recursive = true;
        exComment.Recursive = true;
        exVersion.Recursive = true;
        exAppId.Recursive = true;
        exVendor.Recursive = true;
        exExecutableName.Recursive = true;
        setupLogo.Recursive = true;
        exeCommand.Add(exAppName);
        exeCommand.Add(exComment);
        exeCommand.Add(exVersion);
        exeCommand.Add(exAppId);
        exeCommand.Add(exVendor);
        exeCommand.Add(exExecutableName);
        exeCommand.Add(exArchTop);

        exeCommand.SetAction(async parseResult =>
        {
            var inDir = parseResult.GetValue(exeInputDir)!;
            var outFile = parseResult.GetValue(exeOutput)!;
            var stub = parseResult.GetValue(stubPath);
            var logo = parseResult.GetValue(setupLogo);
            var opt = optionsBinder.Bind(parseResult);
            var vendorOpt = parseResult.GetValue(exVendor);
            var archOpt = parseResult.GetValue(exArchTop);

            await ExecutionWrapper.ExecuteWithLogging("exe", outFile.FullName, async logger =>
            {
                var ridResult = RidUtils.ResolveWindowsRid(archOpt, "EXE packaging");
                if (ridResult.IsFailure)
                {
                    logger.Error("Invalid RID: {Error}", ridResult.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var exeService = new ExePackagingService(logger);
                var result = await exeService.BuildFromDirectory(inDir, outFile, opt, vendorOpt, ridResult.Value, stub, logo);
                if (result.IsFailure)
                {
                    logger.Error("EXE packaging failed: {Error}", result.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                await result.Value.WriteTo(outFile.DirectoryName ?? Directory.GetCurrentDirectory());
                logger.Information("{OutputFile}", outFile.FullName);
            });
        });

        // exe from-project
        var exProject = new Option<FileInfo>("--project")
        {
            Description = "Path to the .csproj file",
            Required = true
        };
        var exArch = new Option<string?>("--arch")
        {
            Description = "Target architecture (x64, arm64)"
        };
        var exSelfContained = new Option<bool>("--self-contained")
        {
            Description = "Publish self-contained"
        };
        exSelfContained.DefaultValueFactory = _ => true;
        var exConfiguration = new Option<string>("--configuration")
        {
            Description = "Build configuration"
        };
        exConfiguration.DefaultValueFactory = _ => "Release";
        var exSingleFile = new Option<bool>("--single-file")
        {
            Description = "Publish single-file"
        };
        var exTrimmed = new Option<bool>("--trimmed")
        {
            Description = "Enable trimming"
        };
        var exOut = new Option<FileInfo>("--output")
        {
            Description = "Output installer .exe",
            Required = true
        };
        var exStub = new Option<FileInfo>("--stub")
        {
            Description = "Path to the prebuilt stub (WinExe) to concatenate (optional if repo layout is present)"
        };
        var exSetupLogo = new Option<FileInfo?>("--setup-logo")
        {
            Description = "Path to a logo image displayed by the installer and uninstaller wizards"
        };

        var exFromProject = new Command("from-project") { Description = "Publish a .NET project and build a Windows self-extracting installer (.exe). If --stub is not provided, the tool downloads the appropriate stub from GitHub Releases." };
        exFromProject.Add(exProject);
        exFromProject.Add(exArch);
        exFromProject.Add(exSelfContained);
        exFromProject.Add(exConfiguration);
        exFromProject.Add(exSingleFile);
        exFromProject.Add(exTrimmed);
        exFromProject.Add(exOut);
        exFromProject.Add(exStub);
        exFromProject.Add(exSetupLogo);

        exFromProject.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(exProject)!;
            var archVal = parseResult.GetValue(exArch);
            var sc = parseResult.GetValue(exSelfContained);
            var cfg = parseResult.GetValue(exConfiguration)!;
            var sf = parseResult.GetValue(exSingleFile);
            var tr = parseResult.GetValue(exTrimmed);
            var extrasOutput = parseResult.GetValue(exOut)!;
            var extrasStub = parseResult.GetValue(exStub);
            var extrasLogo = parseResult.GetValue(exSetupLogo);
            var vendorOpt = parseResult.GetValue(exVendor);
            var opt = optionsBinder.Bind(parseResult);

            await ExecutionWrapper.ExecuteWithLogging("exe-from-project", extrasOutput.FullName, async logger =>
            {
                var ridResult = RidUtils.ResolveWindowsRid(archVal, "EXE packaging");
                if (ridResult.IsFailure)
                {
                    logger.Error("Invalid RID: {Error}", ridResult.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var exeService = new ExePackagingService(logger);
                var result = await exeService.BuildFromProject(prj, ridResult.Value, sc, cfg, sf, tr, extrasOutput, opt, vendorOpt, extrasStub, extrasLogo);
                if (result.IsFailure)
                {
                    logger.Error("EXE from project packaging failed: {Error}", result.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                await result.Value.WriteTo(extrasOutput.DirectoryName ?? Directory.GetCurrentDirectory());
                logger.Information("{OutputFile}", extrasOutput.FullName);
            });
        });

        exeCommand.Add(exFromProject);

        return exeCommand;
    }
}
