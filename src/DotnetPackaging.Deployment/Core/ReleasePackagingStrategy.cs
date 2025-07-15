namespace DotnetPackaging.Deployment.Core;

public class ReleasePackagingStrategy
{
    private readonly Packager packager;

    public ReleasePackagingStrategy(Packager packager)
    {
        this.packager = packager;
    }

    public async Task<Result<IEnumerable<INamedByteSource>>> PackageForPlatforms(ReleaseConfiguration configuration)
    {
        var allFiles = new List<INamedByteSource>();
        // var projectPath = new Path(configuration.ProjectPath);

        // Windows packages
        if (configuration.Platforms.HasFlag(TargetPlatform.Windows))
        {
            var windowsConfig = configuration.WindowsConfig;
            if (windowsConfig == null)
            {
                return Result.Failure<IEnumerable<INamedByteSource>>(
                    "Windows deployment options are required for Windows packaging");
            }

            var windowsResult = await packager.CreateWindowsPackages(windowsConfig.ProjectPath, windowsConfig.Options);
            if (windowsResult.IsFailure)
            {
                return Result.Failure<IEnumerable<INamedByteSource>>(windowsResult.Error);
            }

            allFiles.AddRange(windowsResult.Value);
        }

        // Linux packages
        if (configuration.Platforms.HasFlag(TargetPlatform.Linux))
        {
            var linuxConfig = configuration.LinuxConfig;
            if (linuxConfig == null)
            {
                return Result.Failure<IEnumerable<INamedByteSource>>(
                    "Linux metadata is required for Linux packaging. Provide AppImageMetadata with AppId, AppName, and PackageName");
            }

            var linuxResult = await packager.CreateLinuxPackages(linuxConfig.ProjectPath, linuxConfig.Metadata);
            if (linuxResult.IsFailure)
            {
                return Result.Failure<IEnumerable<INamedByteSource>>(linuxResult.Error);
            }

            allFiles.AddRange(linuxResult.Value);
        }

        // Android packages
        if (configuration.Platforms.HasFlag(TargetPlatform.Android))
        {
            var androidConfig = configuration.AndroidConfig;
            if (androidConfig == null)
            {
                return Result.Failure<IEnumerable<INamedByteSource>>(
                    "Android deployment options are required for Android packaging. Includes signing keys, version codes, etc.");
            }

            var androidResult = await packager.CreateAndroidPackages(androidConfig.ProjectPath, androidConfig.Options);
            if (androidResult.IsFailure)
            {
                return Result.Failure<IEnumerable<INamedByteSource>>(androidResult.Error);
            }

            allFiles.AddRange(androidResult.Value);
        }

        // WebAssembly site
        if (configuration.Platforms.HasFlag(TargetPlatform.WebAssembly))
        {
            var wasmConfig = configuration.WebAssemblyConfig;
            if (wasmConfig == null)
            {
                return Result.Failure<IEnumerable<INamedByteSource>>(
                    "WebAssembly configuration is required for WebAssembly packaging");
            }

            var wasmResult = await packager.CreateWasmSite(wasmConfig.ProjectPath);
            if (wasmResult.IsFailure)
            {
                return Result.Failure<IEnumerable<INamedByteSource>>(wasmResult.Error);
            }

            // Note: WasmApp is typically deployed to GitHub Pages or similar, not included as release asset
            // If you need to include WASM files in release, you'd need a conversion method
        }

        return Result.Success<IEnumerable<INamedByteSource>>(allFiles);
    }
}