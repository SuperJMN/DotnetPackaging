using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Builder;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb;

/// <summary>
/// Debian packager.
/// </summary>
public sealed class DebPackager
{
    /// <summary>
    /// Creates a .deb package from a container and metadata.
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

                var entries = TarEntryBuilder.From(container, packageMetadata, tuple.exec).ToArray();
                var deb = new Archives.Deb.DebFile(packageMetadata, entries);
                return Result.Success<IByteSource>(DebMixin.ToByteSource(deb));
            });
    }
}
