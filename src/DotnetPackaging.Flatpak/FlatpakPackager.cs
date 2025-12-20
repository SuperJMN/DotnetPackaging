using CSharpFunctionalExtensions;
using DotnetPackaging;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Flatpak;

/// <summary>
/// Flatpak packager.
/// </summary>
public sealed class FlatpakPackager
{
    /// <summary>
    /// Creates a Flatpak bundle from a container and metadata.
    /// </summary>
    public Task<Result<IByteSource>> Pack(IContainer container, FlatpakPackagerMetadata metadata, ILogger? logger = null)
    {
        if (container == null)
        {
            throw new ArgumentNullException(nameof(container));
        }

        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        var setup = new FromDirectoryOptions().ApplyOverrides(metadata.PackageOptions);
        var log = logger ?? Log.Logger;
        var flatpakOptions = CopyOptions(metadata.FlatpakOptions);

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

                var planRes = await new FlatpakFactory().BuildPlan(container, packageMetadata, tuple.exec, flatpakOptions);
                if (planRes.IsFailure)
                {
                    return Result.Failure<IByteSource>(planRes.Error);
                }

                return FlatpakBundle.CreateOstree(planRes.Value);
            });
    }

    private static FlatpakOptions CopyOptions(FlatpakOptions source)
    {
        return new FlatpakOptions
        {
            Runtime = source.Runtime,
            Sdk = source.Sdk,
            Branch = source.Branch,
            RuntimeVersion = source.RuntimeVersion,
            Shared = source.Shared.ToArray(),
            Sockets = source.Sockets.ToArray(),
            Devices = source.Devices.ToArray(),
            Filesystems = source.Filesystems.ToArray(),
            ArchitectureOverride = source.ArchitectureOverride,
            CommandOverride = source.CommandOverride
        };
    }
}
