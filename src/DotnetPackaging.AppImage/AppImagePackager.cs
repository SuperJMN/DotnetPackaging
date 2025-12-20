using DotnetPackaging;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Metadata;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage;

/// <summary>
/// AppImage packager.
/// </summary>
public sealed class AppImagePackager
{
    private readonly AppImageFactory factory;

    public AppImagePackager(IRuntimeProvider? runtimeProvider = null)
    {
        factory = new AppImageFactory(runtimeProvider);
    }

    /// <summary>
    /// Creates an AppImage from a container and metadata.
    /// </summary>
    public Task<Result<IByteSource>> Pack(IContainer container, AppImagePackagerMetadata metadata, ILogger? logger = null)
    {
        if (container == null)
        {
            throw new ArgumentNullException(nameof(container));
        }

        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        var log = logger ?? Log.Logger;
        var setup = new FromDirectoryOptions().ApplyOverrides(metadata.PackageOptions);
        var appImageOptions = CopyOptions(metadata.AppImageOptions);

        return BuildUtils.GetExecutable(container, setup, log)
            .Bind(exec => BuildUtils.GetArch(setup, exec).Map(arch => (exec, arch)))
            .Bind(async tuple =>
            {
                var packageMetadata = await BuildUtils.CreateMetadata(
                    setup,
                    container,
                    tuple.arch,
                    tuple.exec,
                    setup.IsTerminal,
                    Maybe<string>.None,
                    log);

                var appImageMetadata = ToAppImageMetadata(packageMetadata, setup);
                var appImageResult = await factory.Create(container, appImageMetadata, tuple.exec, tuple.arch, appImageOptions);
                return await appImageResult.Bind(appImage => appImage.ToByteSource());
            });
    }

    private static AppImageOptions CopyOptions(AppImageOptions source)
    {
        return new AppImageOptions
        {
            IconNameOverride = source.IconNameOverride
        };
    }

    private static AppImageMetadata ToAppImageMetadata(PackageMetadata packageMetadata, FromDirectoryOptions setup)
    {
        var packageName = packageMetadata.Package;
        var appId = packageMetadata.Id.GetValueOrDefault($"com.{packageName.ToLowerInvariant()}");

        return new AppImageMetadata(appId, packageMetadata.Name, packageName)
        {
            Summary = packageMetadata.Summary,
            Comment = packageMetadata.Description,
            Version = Maybe<string>.From(packageMetadata.Version),
            Homepage = packageMetadata.Homepage.Map(u => u.ToString()),
            ProjectLicense = packageMetadata.License,
            Keywords = packageMetadata.Keywords,
            IsTerminal = setup.IsTerminal,
            Categories = packageMetadata.Categories.HasValue
                ? Maybe<IEnumerable<string>>.From(GetCategoryStrings(packageMetadata.Categories.Value))
                : Maybe<IEnumerable<string>>.None
        };
    }

    private static IEnumerable<string> GetCategoryStrings(Categories categories)
    {
        var list = new List<string> { categories.Main.ToString() };
        list.AddRange(categories.AdditionalCategories.Select(c => c.ToString()));
        return list;
    }
}
