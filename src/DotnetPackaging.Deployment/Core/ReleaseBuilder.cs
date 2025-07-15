using DotnetPackaging.Deployment.Platforms.Android;
using DotnetPackaging.Deployment.Platforms.Windows;
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
    
    // Convenience methods for common combinations - now they require explicit project paths
    public ReleaseBuilder ForDesktop(string desktopProjectPath, string packageName, string appId, string appName)
    {
        return ForWindows(desktopProjectPath, packageName)
               .ForLinux(desktopProjectPath, appId, appName, packageName);
    }
    
    // Method for typical Avalonia multi-project setup
    public ReleaseBuilder ForAvaloniaProjects(string baseProjectName, string version, string packageName, string appId, string appName, AndroidDeployment.DeploymentOptions? androidOptions = null)
    {
        var builder = WithVersion(version)
            .ForWindows($"{baseProjectName}.Desktop", packageName)
            .ForLinux($"{baseProjectName}.Desktop", appId, appName, packageName)
            .ForWebAssembly($"{baseProjectName}.Browser");
            
        if (androidOptions != null)
        {
            builder = builder.ForAndroid($"{baseProjectName}.Android", androidOptions);
        }
        
        return builder;
    }
    
    // Method for automatic project discovery based on solution name and Avalonia conventions
    public ReleaseBuilder ForAvaloniaProjectsFromSolution(string solutionPath, string version, string packageName, string appId, string appName, AndroidDeployment.DeploymentOptions? androidOptions = null)
    {
        context.Logger.Information("Starting Avalonia project discovery from solution: {SolutionPath}", solutionPath);
        
        var solutionDirectory = global::System.IO.Path.GetDirectoryName(solutionPath) ?? throw new ArgumentException("Invalid solution path", nameof(solutionPath));
        var solutionName = global::System.IO.Path.GetFileNameWithoutExtension(solutionPath);
        
        context.Logger.Information("Solution directory: {SolutionDirectory}, Solution name: {SolutionName}", solutionDirectory, solutionName);
        
        var builder = WithVersion(version);
        
        // Try to find Desktop project (Windows + Linux)
        context.Logger.Information("Searching for Desktop project...");
        var desktopProject = FindProject(solutionDirectory, solutionName, "Desktop");
        if (desktopProject.HasValue)
        {
            context.Logger.Information("Found Desktop project: {ProjectPath}", desktopProject.Value);
            builder = builder.ForWindows(desktopProject.Value, packageName)
                           .ForLinux(desktopProject.Value, appId, appName, packageName);
        }
        else
        {
            context.Logger.Warn("Desktop project not found");
        }
        
        // Try to find Browser project (WebAssembly)
        context.Logger.Information("Searching for Browser project...");
        var browserProject = FindProject(solutionDirectory, solutionName, "Browser");
        if (browserProject.HasValue)
        {
            context.Logger.Information("Found Browser project: {ProjectPath}", browserProject.Value);
            builder = builder.ForWebAssembly(browserProject.Value);
        }
        else
        {
            context.Logger.Warn("Browser project not found");
        }
        
        // Try to find Android project
        context.Logger.Information("Searching for Android project...");
        var androidProject = FindProject(solutionDirectory, solutionName, "Android");
        if (androidProject.HasValue && androidOptions != null)
        {
            context.Logger.Information("Found Android project: {ProjectPath}", androidProject.Value);
            builder = builder.ForAndroid(androidProject.Value, androidOptions);
        }
        else if (androidProject.HasValue)
        {
            context.Logger.Warn("Found Android project but no Android options provided: {ProjectPath}", androidProject.Value);
        }
        else
        {
            context.Logger.Warn("Android project not found");
        }
        
        context.Logger.Information("Project discovery completed");
        return builder;
    }
    
    private Maybe<string> FindProject(string solutionDirectory, string solutionName, string platformSuffix)
    {
        context.Logger.Debug("Looking for {Platform} project with base name {SolutionName} in directory {Directory}", 
            platformSuffix, solutionName, solutionDirectory);
        
        // Common patterns for Avalonia projects
        var patterns = new[]
        {
            $"{solutionName}.{platformSuffix}",
            $"{solutionName}.{platformSuffix}.csproj",
            $"src/{solutionName}.{platformSuffix}",
            $"src/{solutionName}.{platformSuffix}/{solutionName}.{platformSuffix}.csproj"
        };
        
        foreach (var pattern in patterns)
        {
            context.Logger.Debug("Checking pattern: {Pattern}", pattern);
            var projectPath = global::System.IO.Path.Combine(solutionDirectory, pattern);
            
            // If pattern doesn't end with .csproj, try adding it
            if (!pattern.EndsWith(".csproj"))
            {
                var csprojPath = global::System.IO.Path.Combine(projectPath, $"{solutionName}.{platformSuffix}.csproj");
                context.Logger.Debug("Checking .csproj file: {CsprojPath}", csprojPath);
                if (File.Exists(csprojPath))
                {
                    context.Logger.Debug("Found project file: {ProjectPath}", csprojPath);
                    return Maybe<string>.From(csprojPath);
                }
                else
                {
                    context.Logger.Debug("File not found: {CsprojPath}", csprojPath);
                }
            }
            else 
            {
                context.Logger.Debug("Checking direct path: {ProjectPath}", projectPath);
                if (File.Exists(projectPath))
                {
                    context.Logger.Debug("Found project file: {ProjectPath}", projectPath);
                    return Maybe<string>.From(projectPath);
                }
                else
                {
                    context.Logger.Debug("File not found: {ProjectPath}", projectPath);
                }
            }
        }
        
        context.Logger.Debug("No {Platform} project found with any of the tested patterns", platformSuffix);
        return Maybe<string>.None;
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