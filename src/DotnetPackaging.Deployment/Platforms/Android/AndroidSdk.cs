using static System.IO.Path;

namespace DotnetPackaging.Deployment.Platforms.Android;

public class AndroidSdk(Maybe<ILogger> logger)
{
    public Result<string> FindPath()
    {
        logger.Execute(log => log.Information("Attempting to auto-detect Android SDK..."));

        // Get OS-specific common paths where Android SDK is typically installed
        var commonPaths = GetOsSpecificPaths();

        // Also check environment variables
        var envPath = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ??
                      Environment.GetEnvironmentVariable("ANDROID_HOME");

        var pathsToCheck = envPath != null
            ? new[] { envPath }.Concat(commonPaths).ToList()
            : commonPaths.ToList();

        foreach (var path in pathsToCheck)
        {
            var validationResult = Check(path);
            if (validationResult.IsSuccess)
            {
                logger.Execute(log => log.Information("Android SDK auto-detected at: {Path}", path));
                return validationResult;
            }
        }

        var errorMessage = "Could not auto-detect Android SDK. Verified paths: " + string.Join(", ", pathsToCheck);
        logger.Execute(log => log.Error(errorMessage));
        return Result.Failure<string>(errorMessage);
    }

    private static string[] GetOsSpecificPaths()
    {
        var paths = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            // Windows-specific paths
            paths.AddRange(new[]
            {
                Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Android", "Sdk"),
                Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk"),
                Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Android", "Sdk"),
                Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Android", "Sdk"),
                @"C:\Android\Sdk"
            });
        }
        else if (OperatingSystem.IsMacOS())
        {
            // macOS-specific paths
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            paths.AddRange(new[]
            {
                Combine(homeDir, "Library", "Android", "sdk"),
                Combine(homeDir, "Android", "Sdk"),
                "/usr/local/android-sdk",
                "/opt/android-sdk",
                "/Applications/Android Studio.app/Contents/sdk"
            });
        }
        else if (OperatingSystem.IsLinux())
        {
            // Linux-specific paths
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            paths.AddRange(new[]
            {
                Combine(homeDir, "Android", "Sdk"),
                Combine(homeDir, ".android-sdk"),
                "/usr/lib/android-sdk",
                "/opt/android-sdk",
                "/snap/android-studio/current/android-studio/bin/sdk",
                "/var/lib/snapd/snap/android-studio/current/android-studio/bin/sdk"
            });
        }
        else
        {
            // Fallback for other Unix-like systems
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            paths.AddRange(new[]
            {
                Combine(homeDir, "Android", "Sdk"),
                "/usr/lib/android-sdk",
                "/opt/android-sdk"
            });
        }

        return paths.ToArray();
    }

    public Result<string> Check(string androidSdkPath)
    {
        if (string.IsNullOrWhiteSpace(androidSdkPath))
            return Result.Failure<string>("Android SDK path is empty");

        if (!Directory.Exists(androidSdkPath))
            return Result.Failure<string>($"Android SDK path does not exist: {androidSdkPath}");

        // Verify that it contains typical Android SDK directories
        var requiredDirs = new[] { "platform-tools", "platforms" };
        var missingDirs = requiredDirs.Where(dir => !System.IO.Directory.Exists(Combine(androidSdkPath, dir))).ToList();

        if (missingDirs.Any())
            return Result.Failure<string>($"Path does not appear to be a valid Android SDK. Missing directories: {string.Join(", ", missingDirs)}");

        return Result.Success(androidSdkPath);
    }
}