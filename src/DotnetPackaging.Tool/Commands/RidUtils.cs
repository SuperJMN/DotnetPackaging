using System.Linq;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using RuntimeArchitecture = System.Runtime.InteropServices.Architecture;

namespace DotnetPackaging.Tool.Commands;

public static class RidUtils
{
    public static Result<string> ResolveWindowsRid(string? architecture, string context)
    {
        return ResolveRidForPlatform(architecture, OSPlatform.Windows, "Windows", "win", context);
    }

    public static Result<string> ResolveLinuxRid(string? architecture, string context)
    {
        return ResolveRidForPlatform(architecture, OSPlatform.Linux, "Linux", "linux", context);
    }

    public static Result<string> ResolveMacRid(string? architecture, string context)
    {
        return ResolveRidForPlatform(architecture, OSPlatform.OSX, "macOS", "osx", context);
    }

    private static Result<string> ResolveRidForPlatform(string? architecture, OSPlatform targetPlatform, string targetName, string ridPrefix, string context)
    {
        var architectureResult = DetermineArchitecture(architecture, targetPlatform, targetName, context);
        if (architectureResult.IsFailure)
        {
            return Result.Failure<string>(architectureResult.Error);
        }

        return MapArchitectureToRid(architectureResult.Value, ridPrefix, context);
    }

    private static Result<RuntimeArchitecture> DetermineArchitecture(string? requested, OSPlatform targetPlatform, string targetName, string context)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var parsed = ParseRuntimeArchitecture(requested!);
            if (parsed is RuntimeArchitecture.X64 or RuntimeArchitecture.Arm64)
            {
                return Result.Success(parsed.Value);
            }

            return Result.Failure<RuntimeArchitecture>($"Unsupported architecture '{requested}'. Use x64 or arm64.");
        }

        if (RuntimeInformation.IsOSPlatform(targetPlatform))
        {
            if (RuntimeInformation.OSArchitecture is RuntimeArchitecture.X64 or RuntimeArchitecture.Arm64)
            {
                return Result.Success(RuntimeInformation.OSArchitecture);
            }

            return Result.Failure<RuntimeArchitecture>($"{context} supports x64 or arm64. Detected architecture '{RuntimeInformation.OSArchitecture}' is not supported.");
        }

        return Result.Failure<RuntimeArchitecture>($"--arch is required when building {context} on non-{targetName} hosts (x64/arm64).");
    }

    private static Result<string> MapArchitectureToRid(RuntimeArchitecture architecture, string ridPrefix, string context)
    {
        return architecture switch
        {
            RuntimeArchitecture.X64 => Result.Success($"{ridPrefix}-x64"),
            RuntimeArchitecture.Arm64 => Result.Success($"{ridPrefix}-arm64"),
            _ => Result.Failure<string>($"{context} only supports x64 and arm64.")
        };
    }

    private static RuntimeArchitecture? ParseRuntimeArchitecture(string architecture)
    {
        var normalized = architecture.ToLowerInvariant();
        var archPart = normalized.Split('-').Last();

        return archPart switch
        {
            "x64" or "amd64" => RuntimeArchitecture.X64,
            "arm64" => RuntimeArchitecture.Arm64,
            _ => null
        };
    }
}
