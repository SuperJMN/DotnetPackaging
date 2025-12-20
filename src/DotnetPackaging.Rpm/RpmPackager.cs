using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Rpm.Builder;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Rpm;

/// <summary>
/// RPM packager.
/// </summary>
public sealed class RpmPackager
{
    /// <summary>
    /// Creates an RPM package from a container and metadata.
    /// </summary>
    public Task<Result<IByteSource>> Pack(IContainer container, FromDirectoryOptions metadata, ILogger? logger = null)
    {
        if (container == null)
        {
            throw new ArgumentNullException(nameof(container));
        }

        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        var setup = new FromDirectoryOptions().ApplyOverrides(metadata);
        var log = logger ?? Log.Logger;

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

                var plan = RpmLayoutBuilder.Build(container, packageMetadata, tuple.exec);
                var rpmResult = await Builder.RpmPackager.CreatePackage(packageMetadata, plan);
                return rpmResult.Map(file => (IByteSource)ByteSource.FromStreamFactory(() => File.OpenRead(file.FullName)));
            });
    }
}
