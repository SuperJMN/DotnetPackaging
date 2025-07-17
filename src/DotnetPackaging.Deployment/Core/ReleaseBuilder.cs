using DotnetPackaging.Deployment.Platforms.Android;
using DotnetPackaging.Deployment.Platforms.Windows;
using System.IO;
using System.Text.RegularExpressions;
using Zafiro.Mixins;

namespace DotnetPackaging.Deployment.Core;

public class ReleaseBuilder(Context context)
{
    private readonly ReleaseConfiguration configuration = new();

    public ReleaseBuilder WithVersion(string version)
    {
        configuration.Version = version;
        return this;
    }

    public ReleaseBuilder WithApplicationInfo(string packageName, string appId, string appName)
    {
        configuration.ApplicationInfo = new ApplicationInfo
        {
            PackageName = packageName,
            AppId = appId,
            AppName = appName
        };

        return this;
    }
    
    public ReleaseBuilder ForWindows(string projectPath, WindowsDeployment.DeploymentOptions options)
    {
        configuration.WindowsConfig = new WindowsPlatformConfig
        {
            ProjectPath = projectPath,
            Options = options
        };
        configuration.Platforms |= TargetPlatform.Windows;
        return this;
    }

    public ReleaseBuilder ForWindows(string projectPath)
    {
        var windowsOptions = new WindowsDeployment.DeploymentOptions
        {
            PackageName = configuration.ApplicationInfo.PackageName,
            Version = configuration.Version
        };

        return ForWindows(projectPath, windowsOptions);
    }
    
    public ReleaseBuilder ForWindows(string projectPath, string packageName, string? version = null)
    {
        var windowsOptions = new WindowsDeployment.DeploymentOptions
        {
            PackageName = packageName,
            Version = version ?? configuration.Version
        };
        return ForWindows(projectPath, windowsOptions);
    }
    
    public ReleaseBuilder ForLinux(string projectPath, AppImage.Metadata.AppImageMetadata metadata)
    {
        configuration.LinuxConfig = new LinuxPlatformConfig
        {
            ProjectPath = projectPath,
            Metadata = metadata
        };
        configuration.Platforms |= TargetPlatform.Linux;
        return this;
    }

    public ReleaseBuilder ForLinux(string projectPath)
    {
        var metadata = new AppImage.Metadata.AppImageMetadata(
            configuration.ApplicationInfo.AppId,
            configuration.ApplicationInfo.AppName,
            configuration.ApplicationInfo.PackageName)
        {
            Version = Maybe<string>.From(configuration.Version)
        };

        return ForLinux(projectPath, metadata);
    }
    
    public ReleaseBuilder ForLinux(string projectPath, string appId, string appName, string packageName, string? version = null)
    {
        var metadata = new AppImage.Metadata.AppImageMetadata(appId, appName, packageName)
        {
            Version = Maybe<string>.From(version ?? configuration.Version)
        };
        return ForLinux(projectPath, metadata);
    }
    
    public ReleaseBuilder ForAndroid(string projectPath, AndroidDeployment.DeploymentOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PackageName))
        {
            options.PackageName = configuration.ApplicationInfo.PackageName;
        }

        configuration.AndroidConfig = new AndroidPlatformConfig
        {
            ProjectPath = projectPath,
            Options = options
        };
        configuration.Platforms |= TargetPlatform.Android;
        return this;
    }
    
    public ReleaseBuilder ForWebAssembly(string projectPath)
    {
        configuration.WebAssemblyConfig = new WebAssemblyPlatformConfig
        {
            ProjectPath = projectPath
        };
        configuration.Platforms |= TargetPlatform.WebAssembly;
        return this;
    }
    
    // Convenience methods for common combinations
    public ReleaseBuilder ForDesktop(string desktopProjectPath)
    {
        return ForWindows(desktopProjectPath)
               .ForLinux(desktopProjectPath);
    }

    // Method for typical Avalonia multi-project setup
    public ReleaseBuilder ForAvaloniaProjects(string baseProjectName, string version, AndroidDeployment.DeploymentOptions? androidOptions = null)
    {
        var builder = WithVersion(version)
            .ForWindows($"{baseProjectName}.Desktop")
            .ForLinux($"{baseProjectName}.Desktop")
            .ForWebAssembly($"{baseProjectName}.Browser");
            
        if (androidOptions != null)
        {
            builder = builder.ForAndroid($"{baseProjectName}.Android", androidOptions);
        }
        
        return builder;
    }
    
    // Method for automatic project discovery using the .sln contents rather than file system heuristics
    public ReleaseBuilder ForAvaloniaProjectsFromSolution(string solutionPath, string version, AndroidDeployment.DeploymentOptions? androidOptions = null)
    {
        context.Logger.Information("Starting Avalonia project discovery from solution: {SolutionPath}", solutionPath);

        var solutionDirectory = global::System.IO.Path.GetDirectoryName(solutionPath) ?? throw new ArgumentException("Invalid solution path", nameof(solutionPath));
        var projects = ParseSolutionProjects(solutionPath).ToList();

        context.Logger.Information("Parsed {Count} projects from solution", projects.Count);

        var builder = WithVersion(version);

        // Desktop (Windows + Linux)
        var desktop = projects.FirstOrDefault(p => p.Name.EndsWith(".Desktop", StringComparison.OrdinalIgnoreCase));
        if (desktop != default)
        {
            context.Logger.Information("Found Desktop project: {ProjectPath}", desktop.Path);
            builder = builder.ForWindows(desktop.Path)
                               .ForLinux(desktop.Path);
        }
        else
        {
            context.Logger.Warn("Desktop project not found in solution");
        }

        // Browser (WebAssembly)
        var browser = projects.FirstOrDefault(p => p.Name.EndsWith(".Browser", StringComparison.OrdinalIgnoreCase));
        if (browser != default)
        {
            context.Logger.Information("Found Browser project: {ProjectPath}", browser.Path);
            builder = builder.ForWebAssembly(browser.Path);
        }
        else
        {
            context.Logger.Warn("Browser project not found in solution");
        }

        // Android
        var android = projects.FirstOrDefault(p => p.Name.EndsWith(".Android", StringComparison.OrdinalIgnoreCase));
        if (android != default && androidOptions != null)
        {
            context.Logger.Information("Found Android project: {ProjectPath}", android.Path);
            builder = builder.ForAndroid(android.Path, androidOptions);
        }
        else if (android != default)
        {
            context.Logger.Warn("Android project found but no Android options provided: {ProjectPath}", android.Path);
        }
        else
        {
            context.Logger.Warn("Android project not found in solution");
        }

        context.Logger.Information("Project discovery completed");
        return builder;
    }

    private IEnumerable<(string Name, string Path)> ParseSolutionProjects(string solutionPath)
    {
        var solutionDir = global::System.IO.Path.GetDirectoryName(solutionPath)!;
        foreach (var line in File.ReadLines(solutionPath))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("Project("))
            {
                continue;
            }

            var parts = trimmed.Split(',');
            if (parts.Length < 2)
            {
                continue;
            }

            var nameSection = parts[0];
            var pathSection = parts[1];

            var nameStart = nameSection.IndexOf('"', nameSection.IndexOf('='));
            if (nameStart < 0)
            {
                continue;
            }

            var nameEnd = nameSection.IndexOf('"', nameStart + 1);
            if (nameEnd < 0)
            {
                continue;
            }

            var name = nameSection.Substring(nameStart + 1, nameEnd - nameStart - 1);
            var relative = pathSection.Trim().Trim('"').Replace('\u005c', global::System.IO.Path.DirectorySeparatorChar);
            var fullPath = global::System.IO.Path.GetFullPath(global::System.IO.Path.Combine(solutionDir, relative));
            yield return (name, fullPath);
        }
    }
    
    
    public ReleaseConfiguration Build()
    {
        if (string.IsNullOrWhiteSpace(configuration.Version))
        {
            throw new InvalidOperationException("Version is required. Use WithVersion() first.");
        }
        
        if (configuration.Platforms == TargetPlatform.None)
        {
            throw new InvalidOperationException("At least one platform must be specified.");
        }
        
        return configuration;
    }
}