using System.CommandLine;
using System.CommandLine.Parsing;
using CSharpFunctionalExtensions;
using DotnetPackaging.Flatpak;

namespace DotnetPackaging.Tool.Commands;

public sealed class FlatpakOptionsBinder
{
    private readonly Option<string> runtime;
    private readonly Option<string> sdk;
    private readonly Option<string> branch;
    private readonly Option<string> runtimeVersion;
    private readonly Option<IEnumerable<string>> shared;
    private readonly Option<IEnumerable<string>> sockets;
    private readonly Option<IEnumerable<string>> devices;
    private readonly Option<IEnumerable<string>> filesystems;
    private readonly Option<string?> arch;
    private readonly Option<string?> command;

    public FlatpakOptionsBinder(
        Option<string> runtime,
        Option<string> sdk,
        Option<string> branch,
        Option<string> runtimeVersion,
        Option<IEnumerable<string>> shared,
        Option<IEnumerable<string>> sockets,
        Option<IEnumerable<string>> devices,
        Option<IEnumerable<string>> filesystems,
        Option<string?> arch,
        Option<string?> command)
    {
        this.runtime = runtime;
        this.sdk = sdk;
        this.branch = branch;
        this.runtimeVersion = runtimeVersion;
        this.shared = shared;
        this.sockets = sockets;
        this.devices = devices;
        this.filesystems = filesystems;
        this.arch = arch;
        this.command = command;
    }

    public FlatpakOptions Bind(ParseResult parseResult)
    {
        var archStr = parseResult.GetValue(arch);
        var parsedArch = string.IsNullOrWhiteSpace(archStr) ? null : ParseArchitecture(archStr!);
        return new FlatpakOptions
        {
            Runtime = parseResult.GetValue(runtime)!,
            Sdk = parseResult.GetValue(sdk)!,
            Branch = parseResult.GetValue(branch)!,
            RuntimeVersion = parseResult.GetValue(runtimeVersion)!,
            Shared = parseResult.GetValue(shared)!,
            Sockets = parseResult.GetValue(sockets)!,
            Devices = parseResult.GetValue(devices)!,
            Filesystems = parseResult.GetValue(filesystems)!,
            ArchitectureOverride = parsedArch == null ? Maybe<Architecture>.None : Maybe<Architecture>.From(parsedArch),
            CommandOverride = parseResult.GetValue(command) is { } s && !string.IsNullOrWhiteSpace(s) ? Maybe<string>.From(s) : Maybe<string>.None
        };
    }

    private static Architecture? ParseArchitecture(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        return v switch
        {
            "x86_64" or "amd64" or "x64" => Architecture.X64,
            "aarch64" or "arm64" => Architecture.Arm64,
            "i386" or "x86" => Architecture.X86,
            "armhf" or "arm32" => Architecture.Arm32,
            _ => null
        };
    }
}
