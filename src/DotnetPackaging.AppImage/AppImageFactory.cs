using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Metadata;
using DotnetPackaging;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage;

public class AppImageFactory
{
    private readonly IRuntimeProvider runtimeProvider;

    public AppImageFactory(IRuntimeProvider? runtimeProvider = null)
    {
        this.runtimeProvider = runtimeProvider ?? new DefaultRuntimeProvider();
    }

    public async Task<Result<AppImageContainer>> Create(
        IContainer applicationRoot,
        AppImageMetadata appImageMetadata,
        AppImageOptions? options = null)
    {
        var effectiveOptions = options ?? new AppImageOptions();

        var executableResource = await GetExecutable(applicationRoot);
        if (executableResource.IsFailure)
        {
            return Result.Failure<AppImageContainer>(executableResource.Error);
        }

        var architecture = await executableResource.Value.GetArchitecture();
        if (architecture.IsFailure)
        {
            return Result.Failure<AppImageContainer>(architecture.Error);
        }

        var executable = new Executable
        {
            Resource = executableResource.Value,
            Architecture = architecture.Value,
        };

        var planResult = await BuildPlanInternal(applicationRoot, appImageMetadata, executable.Resource, effectiveOptions);
        if (planResult.IsFailure)
        {
            return Result.Failure<AppImageContainer>(planResult.Error);
        }

        var rootContainer = planResult.Value.ToRootContainer();

        var runtimeResult = await runtimeProvider.Create(executable.Architecture);
        if (runtimeResult.IsFailure)
        {
            return Result.Failure<AppImageContainer>(runtimeResult.Error);
        }

        var unixDirectory = Result.Try(() => rootContainer.ToUnixDirectory(new MetadataResolver(executable)));
        if (unixDirectory.IsFailure)
        {
            return Result.Failure<AppImageContainer>(unixDirectory.Error);
        }

        return Result.Success(new AppImageContainer(runtimeResult.Value, unixDirectory.Value));
    }

    // Build an AppDir container from a raw application directory
    public async Task<Result<IContainer>> BuildAppDir(
        IContainer applicationRoot,
        AppImageMetadata appImageMetadata,
        AppImageOptions? options = null)
    {
        var effectiveOptions = options ?? new AppImageOptions();
        var planResult = await BuildPlan(applicationRoot, appImageMetadata, effectiveOptions);
        if (planResult.IsFailure)
        {
            return Result.Failure<IContainer>(planResult.Error);
        }

        return Result.Success((IContainer)planResult.Value.ToRootContainer());
    }

    public async Task<Result<AppImageBuildPlan>> BuildPlan(
        IContainer applicationRoot,
        AppImageMetadata appImageMetadata,
        AppImageOptions? options = null)
    {
        var effectiveOptions = options ?? new AppImageOptions();
        var executableResult = await GetExecutable(applicationRoot);
        if (executableResult.IsFailure)
        {
            return Result.Failure<AppImageBuildPlan>(executableResult.Error);
        }

        return await BuildPlanInternal(applicationRoot, appImageMetadata, executableResult.Value, effectiveOptions);
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
            var appImage = from rt in runtimeProvider.Create(executable.Architecture)
                             from unixDir in Result.Try(() => appDir.ToUnixDirectory(new MetadataResolver(executable)))
                             select new AppImageContainer(rt, unixDir);
            return appImage;
        });
    }

    private static Task<Result<AppImageBuildPlan>> BuildPlanInternal(
        IContainer applicationRoot,
        AppImageMetadata appImageMetadata,
        INamedByteSource executable,
        AppImageOptions options)
    {
        var executableName = executable.Name;
        var execTargetPath = $"/usr/bin/{executableName}";

        var desktopFile = appImageMetadata.ToDesktopFile(execTargetPath);

        var appRunContent = ByteSource.FromString($"#!/bin/bash\nexec \"$APPDIR/usr/bin/{executableName}\" \"$@\"");
        var desktopContent = ByteSource.FromString(MetadataGenerator.DesktopFileContents(desktopFile));
        var appdataContent = ByteSource.FromString(MetadataGenerator.AppStreamXml(appImageMetadata.ToAppStream()));

        var namedByteSourceWithPaths = applicationRoot.ResourcesWithPathsRecursive().ToList();

        var applicationFiles = namedByteSourceWithPaths
            .ToDictionary(
                file => $"usr/bin/{file.FullPath()}", IByteSource (file) => file,
                StringComparer.Ordinal);

        var files = new Dictionary<string, IByteSource>(StringComparer.Ordinal)
        {
            ["AppRun"] = appRunContent,
            [appImageMetadata.DesktopFileName] = desktopContent,
            [$"usr/share/metainfo/{appImageMetadata.AppDataFileName}"] = appdataContent,
        };

        foreach (var appFile in applicationFiles)
        {
            files[appFile.Key] = appFile.Value;
        }

        var iconName = options.IconNameOverride.GetValueOrDefault(appImageMetadata.IconName);
        var iconPlan = IconDiscovery.Discover(applicationRoot, iconName);

        foreach (var icon in iconPlan.IconFiles)
        {
            if (!files.ContainsKey(icon.Key))
            {
                files[icon.Key] = icon.Value;
            }
        }

        if (iconPlan.DirIcon.HasValue && !files.ContainsKey(".DirIcon"))
        {
            files[".DirIcon"] = iconPlan.DirIcon.Value;
        }

        if (!files.ContainsKey(".DirIcon"))
        {
            var pngIcon = files
                .FirstOrDefault(pair => pair.Key.StartsWith("usr/share/icons/", StringComparison.OrdinalIgnoreCase)
                                        && pair.Key.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .Value;

            if (pngIcon is not null)
            {
                files[".DirIcon"] = pngIcon;
            }
        }

        var planResult = files.ToRootContainer()
            .Map(root => new AppImageBuildPlan(
                executableName,
                execTargetPath,
                iconName,
                appImageMetadata,
                root));

        return Task.FromResult(planResult);
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
