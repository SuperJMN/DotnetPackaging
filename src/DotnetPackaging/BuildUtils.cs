using System.Text.RegularExpressions;
using System.Reactive.Linq;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using DivinePath = Zafiro.DivineBytes.Path;

namespace DotnetPackaging;

public static class BuildUtils
{
    public static async Task<PackageMetadata> CreateMetadata(
        FromDirectoryOptions setup,
        IContainer container,
        Architecture architecture,
        INamedByteSourceWithPath exec,
        bool isTerminal,
        Maybe<string> containerName,
        ILogger? logger = null)
    {
        var log = logger ?? Log.Logger;
        var package = ComputePackageName(setup, exec);
        var iconPlan = IconDiscovery.Discover(container, package.ToLowerInvariant());
        var label = containerName.GetValueOrDefault("container root");

        var iconFiles = new Dictionary<string, IByteSource>(iconPlan.IconFiles, StringComparer.Ordinal);

        Maybe<IIcon> discoveredIcon = Maybe<IIcon>.None;
        if (iconPlan.Png.HasValue)
        {
            var pngResource = iconPlan.Png.Value;
            log.Information("Found icon in resource {Resource}", RelativePath(pngResource));

            var iconResult = await Icon.FromByteSource(pngResource);
            if (iconResult.IsSuccess)
            {
                discoveredIcon = Maybe<IIcon>.From(iconResult.Value);
            }
            else
            {
                log.Warning("Icon autodetection: unable to load PNG '{Resource}': {Error}", RelativePath(pngResource), iconResult.Error);
            }
        }
        else if (iconPlan.Svg.HasValue)
        {
            log.Information("Found icon in resource {Resource}", RelativePath(iconPlan.Svg.Value));
        }
        else
        {
            log.Warning("Icon autodetection: Could not find any icon in '{Label}'. Looked for candidates such as icon.svg, icon.png, icon-256.png", label);
        }

        var dirIcon = iconPlan.DirIcon;

        if (setup.Icon.HasValue)
        {
            var rasterIcon = setup.Icon.Value;
            var iconKey = $"usr/share/icons/hicolor/256x256/apps/{package.ToLowerInvariant()}.png";
            var rasterSource = ByteSource.FromByteObservable(rasterIcon.Bytes);
            iconFiles[iconKey] = rasterSource;
            discoveredIcon = Maybe<IIcon>.From(rasterIcon);
            if (dirIcon.HasNoValue)
            {
                dirIcon = Maybe<IByteSource>.From(rasterSource);
            }
        }

        var svgIcon = iconPlan.Svg.Map(svg => (IByteSource)svg);
        var icon = discoveredIcon;
        var version = setup.Version.GetValueOrDefault("1.0.0");
        var name = setup.ProjectMetadata.HasValue
            ? ApplicationNameResolver.FromProject(setup.Name, setup.ProjectMetadata, exec.Name)
            : ApplicationNameResolver.FromDirectory(setup.Name, exec.Name);

        var packageMetadata = new PackageMetadata(name, architecture, isTerminal, package, version)
        {
            Architecture = architecture,
            Icon = icon,
            SvgIcon = svgIcon,
            IconFiles = iconFiles,
            DirIcon = dirIcon,
            Id = setup.Id,
            Name = name,
            Categories = setup.Categories,
            StartupWmClass = setup.StartupWmClass,
            Comment = setup.Comment,
            Description = ResolveDescription(setup),
            Homepage = setup.Homepage,
            License = setup.License,
            Priority = setup.Priority,
            ScreenshotUrls = setup.ScreenshotUrls,
            Maintainer = ResolveMaintainer(setup),
            Summary = setup.Summary,
            Keywords = setup.Keywords,
            Recommends = setup.Recommends,
            Section = setup.Section,
            Package = package,
            Version = version,
            VcsBrowser = setup.VcsBrowser,
            VcsGit = setup.VcsGit,
            InstalledSize = setup.InstalledSize,
            ModificationTime = setup.ModificationTime.GetValueOrDefault(DateTimeOffset.Now)
        };

        return packageMetadata;
    }

    public static async Task<Result<INamedByteSourceWithPath>> GetExecutable(IContainer container, FromDirectoryOptions setup, ILogger? logger = null)
    {
        var log = logger ?? Log.Logger;

        if (setup.ExecutableName.HasValue)
        {
            var named = await ExecutableLookupByName(container, setup.ExecutableName.Value, log);
            if (named.IsSuccess)
            {
                return named;
            }

            log.Warning("Executable '{ExecName}' not found; falling back to auto-detection: {Error}", setup.ExecutableName.Value, named.Error);
        }

        return await ExecutableLookupWithoutName(container, log);
    }

    public static Task<Result<Architecture>> GetArch(FromDirectoryOptions setup, INamedByteSource exec)
    {
        return setup.Architecture
            .Map(x =>
            {
                if (x == Architecture.All)
                {
                    return Result.Failure<Architecture>("The 'All' architecture is not valid for AppImages since they require an specific AppImage Runtime");
                }

                return Result.Success(x);
            })
            .Or(async () =>
            {
                var architecture = await exec.GetArchitecture();
                return architecture.MapError(err => $"Invalid architecture of file \"{exec.Name}\": {err}");
            })
            .ToResult("Could not determine the architecture")
            .Bind(result => result);
    }

    private static async Task<Result<INamedByteSourceWithPath>> ExecutableLookupByName(IContainer container, string execName, ILogger? logger)
    {
        var log = logger ?? Log.Logger;
        log.Debug("Looking up for executable named '{ExecName}'", execName);

        var resources = container.ResourcesWithPathsRecursive().ToList();
        var result = await Task.FromResult(resources
            .TryFirst(x => string.Equals(x.Name, execName, StringComparison.Ordinal))
            .ToResult($"Could not find executable file '{execName}'"));

        return result.Tap(resource => log.Debug("Executable found successfully at {Path}", RelativePath(resource)));
    }

    private static async Task<Result<INamedByteSourceWithPath>> ExecutableLookupWithoutName(IContainer container, ILogger? logger)
    {
        var log = logger ?? Log.Logger;
        log.Debug("No executable has been specified. Looking up for candidates.");
        var resources = container.ResourcesWithPathsRecursive().ToList();

        var execFiles = await resources
            .ToObservable()
            .Select(resource => Observable.FromAsync(async () => await resource.IsElf())
                .Map(isElf => new { IsElf = isElf, Resource = resource }))
            .Merge(3)
            .Successes()
            .Where(x => x.IsElf && !x.Resource.Name.EndsWith(".so", StringComparison.OrdinalIgnoreCase) && x.Resource.Name != "createdump")
            .Select(x => x.Resource)
            .ToList();

        var ordered = execFiles
            .OrderBy(r => r.FullPath().RouteFragments.Count())
            .ThenBy(r => r.FullPath().ToString().Length)
            .ToList();

        return ordered
            .TryFirst()
            .ToResult("Could not find any executable file in the container root")
            .Tap(resource => log.Debug("Choosing {Executable}", RelativePath(resource)));
    }

    private static string RelativePath(INamedByteSourceWithPath resource)
    {
        return resource.Path == DivinePath.Empty ? resource.Name : resource.FullPath().ToString();
    }

    private static string ComputePackageName(FromDirectoryOptions setup, INamedByteSourceWithPath exec)
    {
        var requested = setup.Package.Or(setup.Name).GetValueOrDefault(exec.Name.Replace(".Desktop", ""));
        return SanitizePackageName(requested);
    }

    private static string SanitizePackageName(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        var replaced = Regex.Replace(lowered, @"[_\s]+", "-");
        var cleaned = Regex.Replace(replaced, @"[^a-z0-9.+-]", string.Empty);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "app";
        }

        return cleaned;
    }

    private static Maybe<string> ResolveMaintainer(FromDirectoryOptions setup)
    {
        if (setup.Maintainer.HasValue)
        {
            return setup.Maintainer;
        }

        return Maybe.From("Unknown Maintainer <unknown@example.com>");
    }

    private static Maybe<string> ResolveDescription(FromDirectoryOptions setup)
    {
        if (setup.Description.HasValue)
        {
            return setup.Description;
        }

        if (setup.Summary.HasValue)
        {
            return setup.Summary;
        }

        if (setup.Comment.HasValue)
        {
            return setup.Comment;
        }

        return Maybe.From("No description provided");
    }
}
