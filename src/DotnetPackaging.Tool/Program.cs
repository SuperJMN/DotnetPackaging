using System.CommandLine;
using System.CommandLine.Parsing;
using DotnetPackaging.Tool.Commands;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace DotnetPackaging.Tool;

static class Program
{
    private const string VerboseEnvVar = "DOTNETPACKAGING_VERBOSE";
    private const string LegacyVerboseEnvVar = "DOTNETPACKAGING_DEBUG";
    
    public static async Task<int> Main(string[] args)
    {
        Environment.ExitCode = 0;
        var verboseEnabled = IsVerboseRequested(args);
        SetVerboseEnvironment(verboseEnabled);
        var normalizedArgs = NormalizeMetadataAliases(args);

        var levelSwitch = new LoggingLevelSwitch(verboseEnabled ? LogEventLevel.Debug : LogEventLevel.Information);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Tool", "DotnetPackaging.Tool")
            .Enrich.WithProperty("Platform", "General")
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {Tool}] {Message:lj}{NewLine}{Exception}", standardErrorFromLevel: LogEventLevel.Verbose)
            .CreateLogger();

        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        
        var rootCommand = new RootCommand
        {
            Description = "Package published .NET applications into Linux-friendly formats.\n\n" +
                          "Available verbs:\n" +
                          "- deb: Build a Debian/Ubuntu .deb installer.\n" +
                          "- rpm: Build an RPM (.rpm) package for Fedora, openSUSE and similar distributions.\n" +
                          "- appimage: Build a portable AppImage (.AppImage) bundle or work with AppDir workflows.\n\n" +
                          "Tip: run `dotnetpackaging <verb> --help` to see format-specific options."
        };

        // Global --verbose option (purely for discoverability; value already read above)
        var verboseOption = new Option<bool>("--verbose", "-v", "--debug", "-d")
        {
            Description = "Enable verbose logging",
            Recursive = true
        };
        rootCommand.Add(verboseOption);

        rootCommand.Add(DebCommand.GetCommand());
        rootCommand.Add(RpmCommand.GetCommand());
        rootCommand.Add(AppImageCommand.GetCommand());
        rootCommand.Add(DmgCommand.GetCommand());
        rootCommand.Add(FlatpakCommand.GetCommand());
        rootCommand.Add(MsixCommand.GetCommand());
        rootCommand.Add(ExeCommand.GetCommand());
        
        var parseResult = rootCommand.Parse(normalizedArgs, configuration: null);
        var exitCode = await parseResult.InvokeAsync(parseResult.InvocationConfiguration, CancellationToken.None);
        var finalExitCode = Environment.ExitCode != 0 ? Environment.ExitCode : exitCode;
        Environment.ExitCode = finalExitCode;

        return finalExitCode;
    }

    private static bool IsVerboseRequested(string[] args)
    {
        if (EnvironmentVariableEnabled(VerboseEnvVar) || EnvironmentVariableEnabled(LegacyVerboseEnvVar))
        {
            return true;
        }

        return args.Any(IsVerboseToken);
    }

    private static bool EnvironmentVariableEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
               || value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVerboseToken(string token)
    {
        return string.Equals(token, "--verbose", StringComparison.OrdinalIgnoreCase)
               || string.Equals(token, "-v", StringComparison.OrdinalIgnoreCase)
               || string.Equals(token, "--debug", StringComparison.OrdinalIgnoreCase)
               || string.Equals(token, "-d", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetVerboseEnvironment(bool verbose)
    {
        Environment.SetEnvironmentVariable(VerboseEnvVar, verbose ? "1" : "0");
    }

    private static string[] NormalizeMetadataAliases(string[] args)
    {
        if (args.Length == 0)
        {
            return args;
        }

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["--productName"] = "--application-name",
            ["--appName"] = "--application-name",
            ["--company"] = "--vendor"
        };

        var normalized = new string[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                var separatorIndex = token.IndexOf('=');
                var key = separatorIndex >= 0 ? token[..separatorIndex] : token;
                if (replacements.TryGetValue(key, out var replacement))
                {
                    normalized[i] = separatorIndex >= 0
                        ? string.Concat(replacement, token[separatorIndex..])
                        : replacement;
                    continue;
                }
            }

            normalized[i] = token;
        }

        return normalized;
    }
}
