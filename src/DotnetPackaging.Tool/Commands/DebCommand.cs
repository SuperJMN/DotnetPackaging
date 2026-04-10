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
        var serviceOption = new Option<bool>("--service") { Description = "Install as a systemd service/daemon" };
        var serviceTypeOption = new Option<string?>("--service-type") { Description = "systemd service type: simple (default), notify, forking, oneshot" };
        var serviceRestartOption = new Option<string?>("--service-restart") { Description = "Restart policy: on-failure (default), always, no, on-abnormal, on-abort" };
        var serviceUserOption = new Option<string?>("--service-user") { Description = "User to run the service as" };
        var serviceEnvironmentOption = new Option<IEnumerable<string>>("--service-environment") { Description = "Environment variables (e.g., DOTNET_ENVIRONMENT=Production)", Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };

        var commands = CommandFactory.CreateCommand(
            "deb",
            "Debian package",
            ".deb",
            CreateDeb,
            "Create a Debian (.deb) installer for Debian and Ubuntu based distributions.",
            null,
            (opts, parseResult) => ApplyServiceOptions(opts, parseResult, serviceOption, serviceTypeOption, serviceRestartOption, serviceUserOption, serviceEnvironmentOption),
            "pack-deb",
            "debian");

        commands.Root.Add(serviceOption);
        commands.Root.Add(serviceTypeOption);
        commands.Root.Add(serviceRestartOption);
        commands.Root.Add(serviceUserOption);
        commands.Root.Add(serviceEnvironmentOption);

        commands.FromDirectory.Add(serviceOption);
        commands.FromDirectory.Add(serviceTypeOption);
        commands.FromDirectory.Add(serviceRestartOption);
        commands.FromDirectory.Add(serviceUserOption);
        commands.FromDirectory.Add(serviceEnvironmentOption);

        AddFromProjectSubcommand(commands.Root, serviceOption, serviceTypeOption, serviceRestartOption, serviceUserOption, serviceEnvironmentOption);
        return commands.Root;
    }

    private static void ApplyServiceOptions(Options opts, ParseResult parseResult, Option<bool> serviceOption, Option<string?> serviceTypeOption, Option<string?> serviceRestartOption, Option<string?> serviceUserOption, Option<IEnumerable<string>> serviceEnvironmentOption)
    {
        var isServiceEnabled = parseResult.GetValue(serviceOption);
        if (!isServiceEnabled) return;

        opts.IsService = true;
        var svcType = parseResult.GetValue(serviceTypeOption);
        if (svcType != null) opts.ServiceType = ParseServiceType(svcType);
        var svcRestart = parseResult.GetValue(serviceRestartOption);
        if (svcRestart != null) opts.ServiceRestart = ParseRestartPolicy(svcRestart);
        var svcUser = parseResult.GetValue(serviceUserOption);
        if (svcUser != null) opts.ServiceUser = svcUser;
        var svcEnv = parseResult.GetValue(serviceEnvironmentOption)?.ToList();
        if (svcEnv != null && svcEnv.Count > 0) opts.ServiceEnvironment = Maybe<IEnumerable<string>>.From(svcEnv);
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

    private static void AddFromProjectSubcommand(Command debCommand, Option<bool> serviceOption, Option<string?> serviceTypeOption, Option<string?> serviceRestartOption, Option<string?> serviceUserOption, Option<IEnumerable<string>> serviceEnvironmentOption)
    {
        var metadata = new MetadataOptionSet();
        var project = new ProjectOptionSet(".deb");

        var fromProject = new Command("from-project") { Description = "Publish a .NET project and build a Debian .deb from the published output." };
        project.AddTo(fromProject);
        metadata.AddTo(fromProject);
        fromProject.Add(serviceOption);
        fromProject.Add(serviceTypeOption);
        fromProject.Add(serviceRestartOption);
        fromProject.Add(serviceUserOption);
        fromProject.Add(serviceEnvironmentOption);

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

            ApplyServiceOptions(opt, parseResult, serviceOption, serviceTypeOption, serviceRestartOption, serviceUserOption, serviceEnvironmentOption);

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

    private static ServiceType ParseServiceType(string value) => value.ToLowerInvariant() switch
    {
        "simple" => ServiceType.Simple,
        "notify" => ServiceType.Notify,
        "forking" => ServiceType.Forking,
        "oneshot" => ServiceType.OneShot,
        "idle" => ServiceType.Idle,
        _ => ServiceType.Simple
    };

    private static RestartPolicy ParseRestartPolicy(string value) => value.ToLowerInvariant() switch
    {
        "no" => RestartPolicy.No,
        "always" => RestartPolicy.Always,
        "on-failure" => RestartPolicy.OnFailure,
        "on-abnormal" => RestartPolicy.OnAbnormal,
        "on-abort" => RestartPolicy.OnAbort,
        "on-watchdog" => RestartPolicy.OnWatchdog,
        _ => RestartPolicy.OnFailure
    };
}
