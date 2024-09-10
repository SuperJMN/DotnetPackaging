using CSharpFunctionalExtensions;
using Serilog;

namespace DotnetPackaging.Deb.Builder;

public class FromContainer
{
    private readonly IDirectory root;
    private readonly FromDirectoryOptions setup;

    public FromContainer(IDirectory root, FromDirectoryOptions setup)
    {
        this.root = root;
        this.setup = setup;
    }

    public Task<Result<Archives.Deb.DebFile>> Build()
    {
        var build = BuildUtils.GetExecutable(root, setup)
            .Bind(exec => BuildUtils.GetArch(setup, exec).Tap(arch => Log.Information("Architecture set to {Arch}", arch))
                .Map(async architecture => new
                {
                    PackageMetadata = await BuildUtils.CreateMetadata(setup, root, architecture, exec, setup.IsTerminal),
                    Executable = exec
                }))
            .Map(conf => new Archives.Deb.DebFile(conf.PackageMetadata, TarEntryBuilder.From(root, conf.PackageMetadata, conf.Executable).ToArray()));

        return build;
    }
}