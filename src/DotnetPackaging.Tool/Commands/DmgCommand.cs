using System.CommandLine;
using System.IO;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Dmg;
using DotnetPackaging.Dmg.Verification;
using Serilog;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using Path = System.IO.Path;

namespace DotnetPackaging.Tool.Commands;

public static class DmgCommand
{
    private static readonly System.IO.Abstractions.FileSystem FileSystem = new();

    public static Command GetCommand()
    {
        // DMG command (experimental, cross-platform)
        var defaultLayoutOption = new Option<bool>("--with-default-layout")
        {
            Description = "Add a default Finder layout (background image, Applications link positioning) when none is provided"
        };
        defaultLayoutOption.DefaultValueFactory = _ => true;

        var dmgCommand = CommandFactory.CreateCommand(
            "dmg",
            "macOS disk image",
            ".dmg",
            CreateDmg,
            "Create a macOS disk image (.dmg) with a native HFS+ payload wrapped in UDIF (DMG) format.",
            defaultLayoutOption,
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
        var dirInfo = FileSystem.DirectoryInfo.New(inputDir.FullName);
        var container = new DirectoryContainer(dirInfo).AsRoot();

        var metadata = new DmgPackagerMetadata
        {
            VolumeName = options.Name.Or(Maybe.From(inputDir.Name)),
            ExecutableName = options.ExecutableName,
            IncludeDefaultLayout = options.UseDefaultLayout,
            Icon = options.Icon,
            Compress = Maybe.From(true),
            AddApplicationsSymlink = Maybe.From(true)
        };

        var packager = new DmgPackager();
        return packager.Pack(container, metadata, logger)
            .Bind(bytes => bytes.WriteTo(outputFile.FullName))
            .WriteResult();
    }

    private static void AddDmgFromProjectSubcommand(Command dmgCommand)
    {
        var project = new Option<FileInfo>("--project") { Description = "Path to the .csproj file", Required = true };
        var arch = new Option<string?>("--arch") { Description = "Target architecture (x64, arm64)" };
        var selfContained = new Option<bool>("--self-contained") { Description = "Publish self-contained [Deprecated]" };
        selfContained.DefaultValueFactory = _ => true;
        var configuration = new Option<string>("--configuration") { Description = "Build configuration" };
        configuration.DefaultValueFactory = _ => "Release";
        var singleFile = new Option<bool>("--single-file") { Description = "Publish single-file" };
        singleFile.DefaultValueFactory = _ => true;
        var trimmed = new Option<bool>("--trimmed") { Description = "Enable trimming" };
        var output = new Option<FileInfo>("--output") { Description = "Output .dmg file", Required = true };
        var compress = new Option<bool>("--compress") { Description = "Compress the DMG payload (bzip2/UDZO-like)" };
        compress.DefaultValueFactory = _ => true;
        var defaultLayoutOption = new Option<bool>("--with-default-layout") { Description = "Add a default Finder layout (background image, Applications link positioning) when none is provided" };
        defaultLayoutOption.DefaultValueFactory = _ => true;

        // Reuse metadata options to get volume name from --application-name if present
        var appName = new Option<string>("--application-name") { Description = "Application name / volume name", Required = false };
        appName.Aliases.Add("--productName");
        appName.Aliases.Add("--appName");
        var dmgIconOption = new Option<IIcon?>("--icon") { Description = "Path to the application icon" };
        dmgIconOption.CustomParser = OptionsBinder.GetIcon;

        var homePage = new Option<Uri>("--homepage") { Description = "Home page of the application", Required = false };
        homePage.CustomParser = OptionsBinder.GetUri;
        var screenshotUrls = new Option<IEnumerable<Uri>>("--screenshot-urls") { Description = "Screenshot URLs", Required = false };
        screenshotUrls.CustomParser = OptionsBinder.GetUris;

        var optionsBinder = new OptionsBinder(appName,
            new Option<string>("--wm-class"),
            new Option<IEnumerable<string>>("--keywords"),
            new Option<string>("--comment"),
            new Option<MainCategory?>("--main-category"),
            new Option<IEnumerable<AdditionalCategory>>("--additional-categories"),
            dmgIconOption,
            new Option<string>("--version"),
            homePage,
            new Option<string>("--license"),
            screenshotUrls,
            new Option<string>("--summary"),
            new Option<string>("--appId"),
            new Option<string>("--executable-name"),
            new Option<bool>("--is-terminal"),
            defaultLayoutOption);

        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build a .dmg from the published output (.app bundle auto-generated if missing). Experimental." };
        fromProject.Add(project);
        fromProject.Add(arch);
        fromProject.Add(selfContained);
        fromProject.Add(configuration);
        fromProject.Add(singleFile);
        fromProject.Add(trimmed);
        fromProject.Add(output);
        fromProject.Add(appName);
        fromProject.Add(compress);
        fromProject.Add(defaultLayoutOption);
        fromProject.Add(homePage);
        fromProject.Add(screenshotUrls);

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
            var compressVal = parseResult.GetValue(compress);
            var useDefaultLayout = opt.UseDefaultLayout.GetValueOrDefault(true);
            var logger = Log.ForContext("command", "dmg-from-project");

            var inferredName = Path.GetFileNameWithoutExtension(prj.Name);
            var icon = await ResolveIcon(opt, prj.Directory!, logger);

            var result = await new Dmg.DmgPackager().PackProject(
                prj.FullName,
                outFile.FullName,
                dmgOpt =>
                {
                    dmgOpt.VolumeName = Maybe.From(opt.Name.GetValueOrDefault(inferredName));
                    dmgOpt.ExecutableName = Maybe.From(opt.ExecutableName.GetValueOrDefault(inferredName));
                    dmgOpt.Compress = Maybe.From(compressVal);
                    dmgOpt.IncludeDefaultLayout = Maybe.From(useDefaultLayout);
                    dmgOpt.Icon = icon;
                },
                pub =>
                {
                    pub.SelfContained = true;
                    pub.Configuration = cfg;
                    pub.SingleFile = sf;
                    pub.Trimmed = tr;
                    if (ridVal != null)
                    {
                        var ridResult = RidUtils.ResolveMacRid(ridVal, "dmg");
                        if (ridResult.IsSuccess) pub.Rid = ridResult.Value;
                    }
                },
                logger);

            result.WriteResult();
        });

        dmgCommand.Add(fromProject);
    }

    private static async Task<Maybe<IIcon>> ResolveIcon(Options options, DirectoryInfo projectDirectory, ILogger logger)
    {
        if (options.Icon.HasValue)
        {
            return Maybe<IIcon>.From(options.Icon.Value);
        }

        var candidate = FindIconCandidate(projectDirectory);
        if (candidate == null)
        {
            return Maybe<IIcon>.None;
        }

        var iconResult = await DotnetPackaging.Icon.FromByteSource(ByteSource.FromStreamFactory(() => File.OpenRead(candidate)));
        if (iconResult.IsFailure)
        {
            logger.Warning("Icon autodiscovery failed for {IconPath}: {Error}", candidate, iconResult.Error);
            return Maybe<IIcon>.None;
        }

        return Maybe<IIcon>.From(iconResult.Value);
    }

    private static string? FindIconCandidate(DirectoryInfo projectDirectory)
    {
        var preferred = new[]
        {
            "icon.icns",
            "app.icns",
            "AppIcon.icns",
            "icon.png",
            "icon-256.png",
            "app.png"
        };

        foreach (var name in preferred)
        {
            var directMatch = Path.Combine(projectDirectory.FullName, name);
            if (File.Exists(directMatch))
            {
                return directMatch;
            }
        }

        var assetsDir = Path.Combine(projectDirectory.FullName, "Assets");
        if (Directory.Exists(assetsDir))
        {
            var assetsIcon = FindIconInDirectory(assetsDir);
            if (assetsIcon != null)
            {
                return assetsIcon;
            }
        }

        var icnsFallback = Directory.EnumerateFiles(projectDirectory.FullName, "*.icns", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (icnsFallback != null)
        {
            return icnsFallback;
        }

        return Directory.EnumerateFiles(projectDirectory.FullName, "*.png", SearchOption.TopDirectoryOnly).FirstOrDefault();
    }

    private static string? FindIconInDirectory(string directory)
    {
        var preferred = new[]
        {
            "icon.icns",
            "app.icns",
            "AppIcon.icns",
            "icon.png",
            "icon-256.png",
            "app.png"
        };

        foreach (var name in preferred)
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var icns = Directory.EnumerateFiles(directory, "*.icns", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (icns != null)
        {
            return icns;
        }

        return Directory.EnumerateFiles(directory, "*.png", SearchOption.TopDirectoryOnly).FirstOrDefault();
    }
}
