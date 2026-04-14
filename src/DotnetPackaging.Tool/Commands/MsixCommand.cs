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
        var msixCommand = new Command("msix") { Description = "MSIX packaging for Windows Store" };
        AddMsixSubcommands(msixCommand);
        return msixCommand;
    }

    private static void AddMsixSubcommands(Command msixCommand)
    {
        var inputDir = new Option<DirectoryInfo>("--directory") { Description = "The input directory (publish output)", Required = true };
        var outputFile = new Option<FileInfo>("--output") { Description = "Output .msix file", Required = true };
        var msixOptions = new MsixOptionSet();

        var packCmd = new Command("from-directory") { Description = "Create an MSIX from a directory. If no AppxManifest.xml exists in the directory, one is generated from the provided options." };
        packCmd.Aliases.Add("pack");
        packCmd.Add(inputDir);
        packCmd.Add(outputFile);
        msixOptions.AddTo(packCmd);
        packCmd.SetAction(async parseResult =>
        {
            var inDir = parseResult.GetValue(inputDir)!;
            var outFile = parseResult.GetValue(outputFile)!;
            await ExecutionWrapper.ExecuteWithLogging("msix-pack", outFile.FullName, async logger =>
            {
                var storeValidation = msixOptions.ValidateForStore(parseResult);
                if (storeValidation.IsFailure)
                {
                    logger.Error(storeValidation.Error);
                    Environment.ExitCode = 1;
                    return;
                }

                var forStore = parseResult.GetValue(msixOptions.ForStore);
                var dirInfo = new System.IO.Abstractions.FileSystem().DirectoryInfo.New(inDir.FullName);
                var container = new DirectoryContainer(dirInfo);
                var metadata = msixOptions.Bind(parseResult);
                var signing = msixOptions.BindSigning(parseResult);
                var icon = msixOptions.BindIcon(parseResult);
                var packager = new MsixPackager();

                var result = await packager.Pack(container, metadata, signing, icon, logger)
                    .Bind(bytes => bytes.WriteTo(outFile.FullName));

                if (result.IsFailure)
                {
                    logger.Error("MSIX packaging failed: {Error}", result.Error);
                    Environment.ExitCode = 1;
                }
                else
                {
                    logger.Information("Success");
                    LogSigningInfo(logger, signing, metadata, forStore);
                }
            });
        });
        msixCommand.Add(packCmd);

        var project = new ProjectOptionSet(".msix");
        var fromProjectOptions = new MsixOptionSet();
        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build a Store-ready MSIX." };
        project.AddTo(fromProject);
        fromProjectOptions.AddTo(fromProject);
        fromProject.SetAction(async parseResult =>
        {
            var prj = parseResult.GetValue(project.Project)!;
            var cfg = parseResult.GetValue(project.Configuration)!;
            var sf = parseResult.GetValue(project.SingleFile);
            var tr = parseResult.GetValue(project.Trimmed);
            var outFile = parseResult.GetValue(project.Output)!;
            var archVal = parseResult.GetValue(project.Arch);
            var logger = Log.ForContext("command", "msix-from-project");

            var storeValidation = fromProjectOptions.ValidateForStore(parseResult);
            if (storeValidation.IsFailure)
            {
                logger.Error(storeValidation.Error);
                Environment.ExitCode = 1;
                return;
            }

            var forStore = parseResult.GetValue(fromProjectOptions.ForStore);
            var metadata = fromProjectOptions.Bind(parseResult);
            var signing = fromProjectOptions.BindSigning(parseResult);
            var icon = fromProjectOptions.BindIcon(parseResult);

            Action<AppManifestMetadata>? metadataConfig = metadata.HasValue
                ? m =>
                {
                    var src = metadata.Value;
                    m.Name = src.Name;
                    m.Publisher = src.Publisher;
                    m.Version = src.Version;
                    m.ProcessorArchitecture = src.ProcessorArchitecture;
                    m.DisplayName = src.DisplayName;
                    m.PublisherDisplayName = src.PublisherDisplayName;
                    m.AppId = src.AppId;
                    m.Executable = src.Executable;
                    m.AppDisplayName = src.AppDisplayName;
                    m.AppDescription = src.AppDescription;
                    m.BackgroundColor = src.BackgroundColor;
                    m.ShortName = src.ShortName;
                }
                : null;

            var result = await new MsixPackager().PackProject(
                prj.FullName,
                outFile.FullName,
                metadataConfig,
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
                signing,
                icon,
                logger);

            result.WriteResult();
            if (result.IsSuccess)
            {
                LogSigningInfo(logger, signing, metadata, forStore);
            }
        });
        msixCommand.Add(fromProject);
    }

    private static void LogSigningInfo(ILogger logger, Maybe<SigningOptions> signing, Maybe<AppManifestMetadata> metadata, bool forStore)
    {
        if (!signing.HasValue)
        {
            logger.Warning("Package was NOT signed. Use --sign to enable signing (default) or provide --pfx for a custom certificate.");
            return;
        }

        var publisher = signing.Value.PublisherCN
            ?? metadata.Map(m => m.Publisher).GetValueOrDefault(MsixOptionSet.DefaultPublisher);
        var usedPfx = signing.Value.PfxPath.HasValue;

        logger.Information("");
        logger.Information("============================================================");

        if (forStore)
        {
            logger.Information("  STORE-READY MSIX PACKAGE");
            logger.Information("============================================================");

            if (usedPfx)
                logger.Information("  Signed with PFX certificate: {PfxPath}", signing.Value.PfxPath.Value);
            else
                logger.Information("  Signed with self-signed certificate.");

            logger.Information("  Publisher: {Publisher}", publisher);
            logger.Information("");
            logger.Information("  This package is ready for Microsoft Store submission.");
            logger.Information("  The Store will replace your signature with its own.");
            logger.Information("  Make sure the Publisher above matches your Partner Center identity.");
        }
        else
        {
            logger.Information("  MSIX PACKAGE (development build)");
            logger.Information("============================================================");

            if (usedPfx)
                logger.Information("  Signed with PFX certificate: {PfxPath}", signing.Value.PfxPath.Value);
            else
                logger.Information("  Signed with self-signed certificate ({Publisher}).", publisher);

            logger.Information("");
            logger.Information("  This package works for development and sideloading.");
            logger.Information("  To submit to the Microsoft Store, add --for-store:");
            logger.Information("");
            logger.Information("    --for-store --publisher \"CN=YOUR-PARTNER-CENTER-PUBLISHER-ID\"");
            logger.Information("");
            logger.Information("  To find your Publisher ID:");
            logger.Information("    1. Go to https://partner.microsoft.com");
            logger.Information("    2. Navigate to your app > Product Identity");
            logger.Information("    3. Copy the 'Package/Identity/Publisher' value");
        }

        logger.Information("");
        logger.Information("  OTHER OPTIONS");
        logger.Information("  Custom certificate:  --pfx mycert.pfx --pfx-password secret");
        logger.Information("  Skip signing:        --sign false");
        logger.Information("  Set app identity:    --appId com.company.myapp --version 1.0.0.0");
        logger.Information("============================================================");
    }
}

public class MsixOptionSet
{
    public Option<string> ApplicationName { get; } = new("--application-name") { Description = "Display name of the application" };
    public Option<string> Publisher { get; } = new("--publisher") { Description = "Publisher identity (e.g. CN=YourName or CN=GUID from Partner Center)", Required = true };
    public Option<string> PublisherDisplayName { get; } = new("--publisher-display-name") { Description = "Publisher display name shown to users" };
    public Option<string> Version { get; } = new("--version") { Description = "Package version (x.y.z.w format)" };
    public Option<string> AppId { get; } = new("--appId") { Description = "Application identity name (reverse DNS, e.g. com.company.app)" };
    public Option<string> ExecutableName { get; } = new("--executable-name") { Description = "Main executable file name (e.g. MyApp.exe)" };
    public Option<string> Description { get; } = new("--description") { Description = "Application description" };
    public Option<FileInfo?> Icon { get; } = new("--icon") { Description = "Source icon (PNG) for generating all visual assets" };
    public Option<string> Arch { get; } = new("--arch") { Description = "Processor architecture: x64, x86, arm64" };
    public Option<FileInfo?> Pfx { get; } = new("--pfx") { Description = "PFX certificate file for signing (optional; self-signed if omitted)" };
    public Option<string?> PfxPassword { get; } = new("--pfx-password") { Description = "Password for PFX certificate" };
    public Option<string> BackgroundColor { get; } = new("--background-color") { Description = "Tile background color (default: transparent)" };
    public Option<bool> Sign { get; } = new("--sign") { Description = "Sign the package (default: true)", DefaultValueFactory = _ => true };
    public Option<bool> ForStore { get; } = new("--for-store") { Description = "Validate that the package is ready for Microsoft Store submission (requires --publisher)" };

    internal const string DefaultPublisher = "CN=DeveloperPackage";

    public MsixOptionSet()
    {
        Publisher.Required = false;
    }

    public void AddTo(Command command)
    {
        command.Add(ApplicationName);
        command.Add(Publisher);
        command.Add(PublisherDisplayName);
        command.Add(Version);
        command.Add(AppId);
        command.Add(ExecutableName);
        command.Add(Description);
        command.Add(Icon);
        command.Add(Arch);
        command.Add(Pfx);
        command.Add(PfxPassword);
        command.Add(BackgroundColor);
        command.Add(Sign);
        command.Add(ForStore);
    }

    public Maybe<AppManifestMetadata> Bind(ParseResult parseResult)
    {
        var publisher = parseResult.GetValue(Publisher);
        var sign = parseResult.GetValue(Sign);

        var effectivePublisher = !string.IsNullOrWhiteSpace(publisher)
            ? publisher
            : sign ? DefaultPublisher : null;

        if (effectivePublisher == null)
            return Maybe<AppManifestMetadata>.None;

        var metadata = new AppManifestMetadata { Publisher = effectivePublisher };

        var name = parseResult.GetValue(ApplicationName);
        if (!string.IsNullOrWhiteSpace(name))
        {
            metadata.DisplayName = name;
            metadata.AppDisplayName = name;
            metadata.ShortName = name;
        }

        var pubDisplay = parseResult.GetValue(PublisherDisplayName);
        if (!string.IsNullOrWhiteSpace(pubDisplay))
            metadata.PublisherDisplayName = pubDisplay;
        else if (!string.IsNullOrWhiteSpace(publisher))
            metadata.PublisherDisplayName = publisher.Replace("CN=", "");

        var version = parseResult.GetValue(Version);
        if (!string.IsNullOrWhiteSpace(version))
            metadata.Version = version;

        var appId = parseResult.GetValue(AppId);
        if (!string.IsNullOrWhiteSpace(appId))
        {
            metadata.Name = appId;
            metadata.AppId = appId.Contains('.') ? appId.Split('.').Last() : appId;
        }

        var exe = parseResult.GetValue(ExecutableName);
        if (!string.IsNullOrWhiteSpace(exe))
            metadata.Executable = exe;

        var desc = parseResult.GetValue(Description);
        if (!string.IsNullOrWhiteSpace(desc))
            metadata.AppDescription = desc;

        var arch = parseResult.GetValue(Arch);
        if (!string.IsNullOrWhiteSpace(arch))
            metadata.ProcessorArchitecture = arch;

        var bgColor = parseResult.GetValue(BackgroundColor);
        if (!string.IsNullOrWhiteSpace(bgColor))
            metadata.BackgroundColor = bgColor;

        return Maybe<AppManifestMetadata>.From(metadata);
    }

    public Maybe<SigningOptions> BindSigning(ParseResult parseResult)
    {
        var publisher = parseResult.GetValue(Publisher);
        var pfx = parseResult.GetValue(Pfx);
        var sign = parseResult.GetValue(Sign);

        if (!sign && pfx == null)
            return Maybe<SigningOptions>.None;

        var effectivePublisher = !string.IsNullOrWhiteSpace(publisher)
            ? publisher
            : DefaultPublisher;

        var options = new SigningOptions
        {
            PublisherCN = effectivePublisher,
            PfxPath = pfx != null ? Maybe<string>.From(pfx.FullName) : Maybe<string>.None,
            PfxPassword = Maybe<string>.From(parseResult.GetValue(PfxPassword) ?? string.Empty),
        };

        return Maybe<SigningOptions>.From(options);
    }

    public Maybe<byte[]> BindIcon(ParseResult parseResult)
    {
        var iconFile = parseResult.GetValue(Icon);
        if (iconFile == null || !iconFile.Exists)
            return Maybe<byte[]>.None;

        return Maybe<byte[]>.From(File.ReadAllBytes(iconFile.FullName));
    }

    public Result ValidateForStore(ParseResult parseResult)
    {
        if (!parseResult.GetValue(ForStore))
            return Result.Success();

        var publisher = parseResult.GetValue(Publisher);
        if (string.IsNullOrWhiteSpace(publisher))
        {
            return Result.Failure(
                "--for-store requires --publisher with your Partner Center identity.\n" +
                "\n" +
                "  To find your Publisher ID:\n" +
                "    1. Go to https://partner.microsoft.com\n" +
                "    2. Navigate to your app > Product Identity\n" +
                "    3. Copy the 'Package/Identity/Publisher' value\n" +
                "\n" +
                "  Example:\n" +
                "    --for-store --publisher \"CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX\"");
        }

        if (publisher == DefaultPublisher)
        {
            return Result.Failure(
                $"--for-store cannot use the default publisher ({DefaultPublisher}).\n" +
                "  Please provide your actual Partner Center publisher identity.");
        }

        return Result.Success();
    }
}
