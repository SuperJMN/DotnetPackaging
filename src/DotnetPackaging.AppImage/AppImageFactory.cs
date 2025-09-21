using System.Reactive.Linq;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Metadata;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage;

public class AppImageFactory
{
    public Task<Result<AppImageContainer>> Create(
        IContainer applicationRoot,
        AppImageMetadata appImageMetadata,
        AppImageOptions? options = null)
    {
        var executableFile =
            from exec in GetExecutable(applicationRoot)
            from arch in exec.GetArchitecture()
            select new Executable
            {
                Resource = exec,
                Architecture = arch,
            };

        return executableFile.Bind(execFile => Create(applicationRoot, appImageMetadata, execFile, options ?? new AppImageOptions()));
    }

    // Build an AppDir (as a RootContainer) from a raw application directory
    public Task<Result<RootContainer>> BuildAppDir(
        IContainer applicationRoot,
        AppImageMetadata appImageMetadata,
        AppImageOptions? options = null)
    {
        var effectiveOptions = options ?? new AppImageOptions();
        var appDirResult = from exec in GetExecutable(applicationRoot)
                           from root in BuildAppDirInternal(applicationRoot, appImageMetadata, exec, effectiveOptions)
                           select root;
        return appDirResult;
    }

    private static Task<Result<RootContainer>> BuildAppDirInternal(
        IContainer applicationRoot,
        AppImageMetadata appImageMetadata,
        INamedByteSource executable,
        AppImageOptions options)
    {
        var executableName = executable.Name;

        var desktopFile = appImageMetadata.ToDesktopFile($"/usr/bin/{executableName}");

        var appRunContent = ByteSource.FromString($"#!/bin/bash\nexec \"$APPDIR/usr/bin/{executableName}\" \"$@\"");
        var desktopContent = ByteSource.FromString(MetadataGenerator.DesktopFileContents(desktopFile));
        var appdataContent = ByteSource.FromString(MetadataGenerator.AppStreamXml(appImageMetadata.ToAppStream()));

        // Get all application files with their relative paths
        var namedByteSourceWithPaths = applicationRoot.ResourcesWithPathsRecursive();

        var applicationFiles = namedByteSourceWithPaths
            .ToDictionary(
                file => $"usr/bin/{file.FullPath()}", IByteSource (file) => file);

        var files = new Dictionary<string, IByteSource>
        {
            ["AppRun"] = appRunContent,
            [appImageMetadata.DesktopFileName] = desktopContent,
            [$"usr/share/metainfo/{appImageMetadata.AppDataFileName}"] = appdataContent,
        };

        // Add all application files to the files dictionary
        foreach (var appFile in applicationFiles)
        {
            files[appFile.Key] = appFile.Value;
        }

        // Icon discovery and installation
        var iconName = options.IconNameOverride.GetValueOrDefault(appImageMetadata.IconName);
        var iconFiles = Icons.IconInstaller.Discover(applicationRoot, appImageMetadata, iconName, options.EnableDirIcon);
        foreach (var icon in iconFiles)
        {
            files[icon.Key] = icon.Value;
        }

        // Fallback: if no icons discovered, try root-only quick check (icon.svg, icon-256.png, icon.png)
        if (!iconFiles.Any())
        {
            var all = namedByteSourceWithPaths.ToList();
            bool IsRoot(INamedByteSource f)
            {
                var fp = ((INamedWithPath)f).FullPath().ToString();
                return !fp.Contains('/') && !fp.Contains('\\');
            }
            var roots = all.Where(IsRoot).ToList();
            INamedByteSource? svg = roots.FirstOrDefault(f => f.Name.Equals("icon.svg", StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith("-icon.svg", StringComparison.OrdinalIgnoreCase));
            INamedByteSource? png256 = roots.FirstOrDefault(f => f.Name.Equals("icon-256.png", StringComparison.OrdinalIgnoreCase));
            INamedByteSource? pngAny = png256 ?? roots.FirstOrDefault(f => f.Name.Equals("icon.png", StringComparison.OrdinalIgnoreCase));

            if (svg != null)
            {
                files[$"usr/share/icons/hicolor/scalable/apps/{appImageMetadata.IconName}.svg"] = svg;
            }
            if (pngAny != null)
            {
                files[$"usr/share/icons/hicolor/256x256/apps/{appImageMetadata.IconName}.png"] = pngAny;
                if (options.EnableDirIcon)
                {
                    files[".DirIcon"] = pngAny;
                }
            }
        }

        return Task.FromResult(files.ToRootContainer());
    }

    // New: create directly from an AppDir-shaped container (no file synthesis, no icon heuristics)
    public Task<Result<AppImageContainer>> CreateFromAppDir(
        IContainer appDir,
        AppImageMetadata appImageMetadata,
        string? executableRelativePath = null,
        Architecture? architectureOverride = null)
    {
        var execResult = from exec in GetExecutableFromAppDir(appDir, executableRelativePath)
                         from arch in architectureOverride != null
                             ? Task.FromResult(Result.Success(architectureOverride))
                             : exec.GetArchitecture()
                         select new Executable
                         {
                             Resource = exec,
                             Architecture = arch,
                         };

        return execResult.Bind(executable =>
        {
            // Use the appDir container as-is; assume it already contains AppRun, .desktop, icons, etc.
            var appImage = from rt in RuntimeFactory.Create(executable.Architecture)
                           from unixDir in Result.Try(() => new Container("", appDir.Resources, appDir.Subcontainers).ToUnixDirectory(new MetadataResolver(executable)))
                           select new AppImageContainer(rt, unixDir);
            return appImage;
        });
    }

    private static async Task<Result<INamedByteSourceWithPath>> GetExecutableFromAppDir(IContainer appDir, string? executableRelativePath)
    {
        if (!string.IsNullOrWhiteSpace(executableRelativePath))
        {
            var normalizedTarget = executableRelativePath.Replace('\\', '/');
            var found = appDir.ResourcesWithPathsRecursive()
                .FirstOrDefault(s => ((INamedWithPath)s).FullPath().ToString().Replace('\\', '/')
                    .Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase));
            return found is not null
                ? Result.Success(found)
                : Result.Failure<INamedByteSourceWithPath>($"Executable '{executableRelativePath}' not found in AppDir");
        }

        // Default: pick first ELF under usr/bin, else first ELF anywhere
        var all = appDir.ResourcesWithPathsRecursive().ToList();
        var underUsrBin = all.Where(s => ((INamedWithPath)s).FullPath().ToString().Replace('\\', '/').StartsWith("usr/bin/", StringComparison.OrdinalIgnoreCase));

        foreach (var s in underUsrBin.Concat(all))
        {
            var elfCheck = await s.IsElf();
            if (elfCheck.IsSuccess && elfCheck.Value)
            {
                return Result.Success(s);
            }
        }

        return Result.Failure<INamedByteSourceWithPath>("No ELF executable found in the AppDir (looked under usr/bin and then anywhere)");
    }

    private static Task<Result<AppImageContainer>> Create(IContainer applicationRoot, AppImageMetadata appImageMetadata, Executable executableFile, AppImageOptions options)
    {
        var executableName = executableFile.Resource.Name;

        var desktopFile = appImageMetadata.ToDesktopFile($"/usr/bin/{executableName}");

        var appRunContent = ByteSource.FromString($"#!/bin/bash\nexec \"$APPDIR/usr/bin/{executableName}\" \"$@\"");
        var desktopContent = ByteSource.FromString(MetadataGenerator.DesktopFileContents(desktopFile));
        var appdataContent = ByteSource.FromString(MetadataGenerator.AppStreamXml(appImageMetadata.ToAppStream()));

        // Get all application files with their relative paths
        var namedByteSourceWithPaths = applicationRoot.ResourcesWithPathsRecursive();

        var applicationFiles = namedByteSourceWithPaths
            .ToDictionary(
                file => $"usr/bin/{file.FullPath()}", IByteSource (file) => file);

        var files = new Dictionary<string, IByteSource>
        {
            ["AppRun"] = appRunContent,
            [appImageMetadata.DesktopFileName] = desktopContent,
            [$"usr/share/metainfo/{appImageMetadata.AppDataFileName}"] = appdataContent,
        };

        // Add all application files to the files dictionary
        foreach (var appFile in applicationFiles)
        {
            files[appFile.Key] = appFile.Value;
        }

        // Icon discovery and installation
        var iconName = options.IconNameOverride.GetValueOrDefault(appImageMetadata.IconName);
        var iconFiles = Icons.IconInstaller.Discover(applicationRoot, appImageMetadata, iconName, options.EnableDirIcon);
        foreach (var icon in iconFiles)
        {
            files[icon.Key] = icon.Value;
        }

        // Fallback: if no icons discovered, try root-only quick check (icon.svg, icon-256.png, icon.png)
        if (!iconFiles.Any())
        {
            var all = namedByteSourceWithPaths.ToList();
            bool IsRoot(INamedByteSource f)
            {
                var fp = ((INamedWithPath)f).FullPath().ToString();
                return !fp.Contains('/') && !fp.Contains('\\');
            }
            var roots = all.Where(IsRoot).ToList();
            INamedByteSource? svg = roots.FirstOrDefault(f => f.Name.Equals("icon.svg", StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith("-icon.svg", StringComparison.OrdinalIgnoreCase));
            INamedByteSource? png256 = roots.FirstOrDefault(f => f.Name.Equals("icon-256.png", StringComparison.OrdinalIgnoreCase));
            INamedByteSource? pngAny = png256 ?? roots.FirstOrDefault(f => f.Name.Equals("icon.png", StringComparison.OrdinalIgnoreCase));

            if (svg != null)
            {
                files[$"usr/share/icons/hicolor/scalable/apps/{iconName}.svg"] = svg;
            }
            if (pngAny != null)
            {
                files[$"usr/share/icons/hicolor/256x256/apps/{iconName}.png"] = pngAny;
                if (options.EnableDirIcon)
                {
                    files[".DirIcon"] = pngAny;
                }
            }
        }

        var rootContainer = files.ToRootContainer();

        var appImage = from rt in RuntimeFactory.Create(executableFile.Architecture)
                       from rootCont in rootContainer
                       from unixDir in Result.Try(() => rootCont.AsContainer().ToUnixDirectory(new MetadataResolver(executableFile)))
                       select new AppImageContainer(rt, unixDir);

        return appImage;
    }

    private static Task<Result<INamedByteSource>> GetExecutable(IContainer applicationRoot)
    {
        return applicationRoot.Resources
            .Where(source => source.Name != "createdump" && !source.Name.EndsWith(".so"))
            .TryFirstResult(async source => await source.IsElf())
            .ToResult("No ELF executable found in the application root directory");
    }
}

public class Executable
{
    public INamedByteSource Resource { get; set; }
    public Architecture Architecture { get; set; }
}
