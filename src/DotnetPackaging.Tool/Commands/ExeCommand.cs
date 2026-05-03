using System.IO;
using System.CommandLine;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Exe;
using DotnetPackaging.Exe.Metadata;
using Serilog;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using Directory = System.IO.Directory;
using File = System.IO.File;
using System.IO.Abstractions;

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
        var exVendor = new Option<string>("--vendor")
        {
            Description = "Vendor/Publisher",
            Required = false
        };
        exVendor.Aliases.Add("--company");
        var pfxOption = new Option<FileInfo?>("--pfx")
        {
            Description = "Path to a PFX code signing certificate. All PE files and the final installer will be Authenticode-signed."
        };
        pfxOption.Recursive = true;
        var pfxPasswordOption = new Option<string?>("--pfx-password")
        {
            Description = "Password for the PFX certificate file"
        };
        pfxPasswordOption.Recursive = true;

        var metadata = new MetadataOptionSet();
        var optionsBinder = metadata.CreateBinder();

        exeCommand.Add(exeInputDir);
        exeCommand.Add(exeOutput);
        exeCommand.Add(stubPath);
        exeCommand.Add(setupLogo);
        metadata.ApplicationName.Recursive = true;
        metadata.Comment.Recursive = true;
        metadata.Version.Recursive = true;
        metadata.AppId.Recursive = true;
        exVendor.Recursive = true;
        metadata.ExecutableName.Recursive = true;
        setupLogo.Recursive = true;
        metadata.AddTo(exeCommand);
        exeCommand.Add(exVendor);
        exeCommand.Add(exArchTop);
        exeCommand.Add(pfxOption);
        exeCommand.Add(pfxPasswordOption);

        exeCommand.SetAction(async parseResult =>
        {
            Console.Error.WriteLine("Warning: 'dotnetpackager exe --directory' is deprecated and will be removed in a future version. Use 'dotnetpackager exe from-directory' instead.");
            var inDir = parseResult.GetValue(exeInputDir)!;
            var outFile = parseResult.GetValue(exeOutput)!;
            var stub = parseResult.GetValue(stubPath);
            var logo = parseResult.GetValue(setupLogo);
            var opt = optionsBinder.Bind(parseResult);
            var vendorOpt = parseResult.GetValue(exVendor);
            var archOpt = parseResult.GetValue(exArchTop);
            var pfx = parseResult.GetValue(pfxOption);
            var pfxPwd = parseResult.GetValue(pfxPasswordOption);

            await ExecutionWrapper.ExecuteWithLogging("exe", outFile.FullName, async logger =>
            {
                var ridResult = RidUtils.ResolveWindowsRid(archOpt, "EXE packaging");
                if (ridResult.IsFailure)
                {
                    logger.Error("Invalid architecture: {Error}", ridResult.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var containerResult = new DirectoryContainer(new DirectoryInfoWrapper(new FileSystem(), inDir)).AsRoot();

                var stubBytes = stub != null
                    ? FileByteSource.OpenRead(stub)
                    : null;

                var logoBytes = logo != null
                    ? FileByteSource.OpenRead(logo)
                    : null;

                var packager = new ExePackager(logger: logger);
                var exeMetadata = new ExePackagerMetadata
                {
                    Options = opt,
                    Vendor = Maybe.From(vendorOpt),
                    RuntimeIdentifier = Maybe.From(ridResult.Value),
                    Stub = stubBytes == null ? Maybe<IByteSource>.None : Maybe.From(stubBytes),
                    SetupLogo = logoBytes == null ? Maybe<IByteSource>.None : Maybe.From(logoBytes),
                    OutputName = Maybe.From(outFile.Name),
                    PfxPath = pfx != null ? Maybe.From(pfx.FullName) : Maybe<string>.None,
                    PfxPassword = pfxPwd != null ? Maybe.From(pfxPwd) : Maybe<string>.None
                };

                var result = await packager.Pack(containerResult, exeMetadata);
                if (result.IsFailure)
                {
                    logger.Error("EXE packaging failed: {Error}", result.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var writeResult = await result.Value.WriteTo(outFile.FullName);
                if (writeResult.IsFailure)
                {
                    logger.Error("Failed to persist installer: {Error}", writeResult.Error);
                    Environment.ExitCode = 1;
                    return;
                }
                logger.Information("{OutputFile}", outFile.FullName);
            });
        });

        // exe from-directory (canonical path)
        var fdInputDir = new Option<DirectoryInfo>("--directory")
        {
            Description = "The input directory (publish output)",
            Required = true
        };
        var fdOutput = new Option<FileInfo>("--output")
        {
            Description = "Output installer .exe",
            Required = true
        };
        var fdStub = new Option<FileInfo>("--stub")
        {
            Description = "Path to the prebuilt stub (WinExe) to concatenate (optional if repo layout is present)"
        };
        var fdSetupLogo = new Option<FileInfo?>("--setup-logo")
        {
            Description = "Path to a logo image displayed by the installer and uninstaller wizards"
        };
        var fdArch = new Option<string?>("--arch")
        {
            Description = "Target architecture for the stub (x64, arm64)"
        };
        var fdMetadata = new MetadataOptionSet();
        var fdBinder = fdMetadata.CreateBinder();

        var fromDirectory = new Command("from-directory") { Description = "Create a Windows self-extracting installer (.exe) from a published application directory." };
        fromDirectory.Add(fdInputDir);
        fromDirectory.Add(fdOutput);
        fromDirectory.Add(fdStub);
        fromDirectory.Add(fdSetupLogo);
        fromDirectory.Add(exVendor);
        fromDirectory.Add(fdArch);
        fromDirectory.Add(pfxOption);
        fromDirectory.Add(pfxPasswordOption);
        fdMetadata.AddTo(fromDirectory);

        fromDirectory.SetAction(async parseResult =>
        {
            var inDir = parseResult.GetValue(fdInputDir)!;
            var outFile = parseResult.GetValue(fdOutput)!;
            var stub = parseResult.GetValue(fdStub);
            var logo = parseResult.GetValue(fdSetupLogo);
            var opt = fdBinder.Bind(parseResult);
            var vendorOpt = parseResult.GetValue(exVendor);
            var archOpt = parseResult.GetValue(fdArch);
            var pfx = parseResult.GetValue(pfxOption);
            var pfxPwd = parseResult.GetValue(pfxPasswordOption);

            await ExecutionWrapper.ExecuteWithLogging("exe", outFile.FullName, async logger =>
            {
                var ridResult = RidUtils.ResolveWindowsRid(archOpt, "EXE packaging");
                if (ridResult.IsFailure)
                {
                    logger.Error("Invalid architecture: {Error}", ridResult.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var containerResult = new DirectoryContainer(new DirectoryInfoWrapper(new FileSystem(), inDir)).AsRoot();

                var stubBytes = stub != null
                    ? FileByteSource.OpenRead(stub)
                    : null;

                var logoBytes = logo != null
                    ? FileByteSource.OpenRead(logo)
                    : null;

                var packager = new ExePackager(logger: logger);
                var exeMetadata = new ExePackagerMetadata
                {
                    Options = opt,
                    Vendor = Maybe.From(vendorOpt),
                    RuntimeIdentifier = Maybe.From(ridResult.Value),
                    Stub = stubBytes == null ? Maybe<IByteSource>.None : Maybe.From(stubBytes),
                    SetupLogo = logoBytes == null ? Maybe<IByteSource>.None : Maybe.From(logoBytes),
                    OutputName = Maybe.From(outFile.Name),
                    PfxPath = pfx != null ? Maybe.From(pfx.FullName) : Maybe<string>.None,
                    PfxPassword = pfxPwd != null ? Maybe.From(pfxPwd) : Maybe<string>.None
                };

                var result = await packager.Pack(containerResult, exeMetadata);
                if (result.IsFailure)
                {
                    logger.Error("EXE packaging failed: {Error}", result.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var writeResult = await result.Value.WriteTo(outFile.FullName);
                if (writeResult.IsFailure)
                {
                    logger.Error("Failed to persist installer: {Error}", writeResult.Error);
                    Environment.ExitCode = 1;
                    return;
                }
                logger.Information("{OutputFile}", outFile.FullName);
            });
        });

        exeCommand.Add(fromDirectory);

        // exe from-project
        var project = new ProjectOptionSet(".exe");
        var exStub = new Option<FileInfo>("--stub")
        {
            Description = "Path to the prebuilt stub (WinExe) to concatenate (optional if repo layout is present)"
        };
        var exSetupLogo = new Option<FileInfo?>("--setup-logo")
        {
            Description = "Path to a logo image displayed by the installer and uninstaller wizards"
        };

        var exFromProject = new Command("from-project") { Description = "Publish a .NET project and build a Windows self-extracting installer (.exe). If --stub is not provided, the tool downloads the appropriate stub from GitHub Releases." };
        project.AddTo(exFromProject);
        exFromProject.Add(exStub);
        exFromProject.Add(exSetupLogo);
        exFromProject.Add(pfxOption);
        exFromProject.Add(pfxPasswordOption);

        exFromProject.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(project.Project)!;
            var archVal = parseResult.GetValue(project.Arch);
            var cfg = parseResult.GetValue(project.Configuration)!;
            var sf = parseResult.GetValue(project.SingleFile);
            var tr = parseResult.GetValue(project.Trimmed);
            var extrasOutput = parseResult.GetValue(project.Output)!;
            var extrasStub = parseResult.GetValue(exStub);
            var extrasLogo = parseResult.GetValue(exSetupLogo);
            var vendorOpt = parseResult.GetValue(exVendor);
            var pfx = parseResult.GetValue(pfxOption);
            var pfxPwd = parseResult.GetValue(pfxPasswordOption);
            var opt = optionsBinder.Bind(parseResult);
            var logger = Log.ForContext("command", "exe-from-project");

            var stubBytes = extrasStub != null
                ? FileByteSource.OpenRead(extrasStub)
                : null;

            var logoBytes = extrasLogo != null
                ? FileByteSource.OpenRead(extrasLogo)
                : null;

            var ridHint = Maybe<string>.None;
            if (archVal != null)
            {
                var ridResult = RidUtils.ResolveWindowsRid(archVal, "exe");
                if (ridResult.IsSuccess)
                {
                    ridHint = Maybe.From(ridResult.Value);
                }
            }

            var result = await new Exe.ExePackager().PackProject(
                prj.FullName,
                extrasOutput.FullName,
                exeMetadata =>
                {
                    exeMetadata.Options = opt;
                    exeMetadata.Vendor = Maybe.From(vendorOpt);
                    exeMetadata.Stub = stubBytes == null ? Maybe<IByteSource>.None : Maybe.From(stubBytes);
                    exeMetadata.SetupLogo = logoBytes == null ? Maybe<IByteSource>.None : Maybe.From(logoBytes);
                    exeMetadata.RuntimeIdentifier = ridHint;
                    exeMetadata.OutputName = Maybe.From(extrasOutput.Name);
                    exeMetadata.PfxPath = pfx != null ? Maybe.From(pfx.FullName) : Maybe<string>.None;
                    exeMetadata.PfxPassword = pfxPwd != null ? Maybe.From(pfxPwd) : Maybe<string>.None;
                },
                pub =>
                {
                    pub.SelfContained = true;
                    pub.Configuration = cfg;
                    pub.SingleFile = sf;
                    pub.Trimmed = tr;
                    if (archVal != null)
                    {
                        var ridResult = RidUtils.ResolveWindowsRid(archVal, "exe");
                        if (ridResult.IsSuccess) pub.Rid = ridResult.Value;
                    }
                },
                logger);

            result.WriteResult();
        });

        exeCommand.Add(exFromProject);

        return exeCommand;
    }

}
