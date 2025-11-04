using CSharpFunctionalExtensions;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Rpm.Builder;

public class FromContainer
{
    private readonly IContainer root;
    private readonly FromDirectoryOptions setup;
    private readonly Maybe<string> containerName;

    public FromContainer(IContainer root, FromDirectoryOptions setup, Maybe<string> containerName)
    {
        this.root = root;
        this.setup = setup;
        this.containerName = containerName;
    }

    public async Task<Result<RpmPackage>> Build()
    {
        var executableResult = await BuildUtils.GetExecutable(root, setup);
        if (executableResult.IsFailure)
        {
            return Result.Failure<RpmPackage>(executableResult.Error);
        }

        var executable = executableResult.Value;

        var architectureResult = await BuildUtils.GetArch(setup, executable);
        if (architectureResult.IsFailure)
        {
            return Result.Failure<RpmPackage>(architectureResult.Error);
        }

        var architecture = architectureResult.Value;
        Log.Information("Architecture set to {Arch}", architecture);

        var metadata = await BuildUtils.CreateMetadata(setup, root, architecture, executable, setup.IsTerminal, containerName);
        var entries = RpmLayoutBuilder.Build(root, metadata, executable);

        return await RpmPackager.CreatePackage(metadata, entries);
    }
}
