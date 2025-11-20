using Serilog;

namespace DotnetPackaging.Exe.Installer.Core;

internal static class LoggerSetup
{
    public static void ConfigureLogger(bool isUninstaller)
    {
        var logDirectory = Path.Combine(Path.GetTempPath(), "DotnetPackaging.Installer");
        Directory.CreateDirectory(logDirectory);

        var logFileName = isUninstaller ? "uninstaller.log" : "installer.log";
        var logPath = Path.Combine(logDirectory, logFileName);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Logger initialized. Mode: {Mode}, Log: {LogPath}", 
            isUninstaller ? "Uninstaller" : "Installer", 
            logPath);
    }
}
