using System.CommandLine;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Tool.Commands;

public sealed class ServiceOptionSet
{
    public Option<bool> Service { get; } = new("--service") { Description = "Install as a systemd service/daemon" };
    public Option<string?> ServiceType { get; } = new("--service-type") { Description = "systemd service type: simple (default), notify, forking, oneshot" };
    public Option<string?> ServiceRestart { get; } = new("--service-restart") { Description = "Restart policy: on-failure (default), always, no, on-abnormal, on-abort" };
    public Option<string?> ServiceUser { get; } = new("--service-user") { Description = "User to run the service as" };
    public Option<IEnumerable<string>> ServiceEnvironment { get; } = new("--service-environment") { Description = "Environment variables (e.g., DOTNET_ENVIRONMENT=Production)", Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };

    public void AddTo(Command command)
    {
        command.Add(Service);
        command.Add(ServiceType);
        command.Add(ServiceRestart);
        command.Add(ServiceUser);
        command.Add(ServiceEnvironment);
    }

    public void Apply(Options opts, ParseResult parseResult)
    {
        var isServiceEnabled = parseResult.GetValue(Service);
        if (!isServiceEnabled) return;

        opts.IsService = true;
        var svcType = parseResult.GetValue(ServiceType);
        if (svcType != null) opts.ServiceType = ParseServiceType(svcType);
        var svcRestart = parseResult.GetValue(ServiceRestart);
        if (svcRestart != null) opts.ServiceRestart = ParseRestartPolicy(svcRestart);
        var svcUser = parseResult.GetValue(ServiceUser);
        if (svcUser != null) opts.ServiceUser = svcUser;
        var svcEnv = parseResult.GetValue(ServiceEnvironment)?.ToList();
        if (svcEnv != null && svcEnv.Count > 0) opts.ServiceEnvironment = Maybe<IEnumerable<string>>.From(svcEnv);
    }

    private static DotnetPackaging.ServiceType ParseServiceType(string value) => value.ToLowerInvariant() switch
    {
        "simple" => DotnetPackaging.ServiceType.Simple,
        "notify" => DotnetPackaging.ServiceType.Notify,
        "forking" => DotnetPackaging.ServiceType.Forking,
        "oneshot" => DotnetPackaging.ServiceType.OneShot,
        "idle" => DotnetPackaging.ServiceType.Idle,
        _ => DotnetPackaging.ServiceType.Simple
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
