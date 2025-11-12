using CSharpFunctionalExtensions;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Rpm.Builder;

public class FromContainer
{
    private readonly IContainer root;
    private readonly FromDirectoryOptions setup;
    private readonly Maybe<string> containerName;
    private readonly ILogger logger;

    public FromContainer(IContainer root, FromDirectoryOptions setup, Maybe<string> containerName, ILogger? logger = null)
    {
        this.root = root;
        this.setup = setup;
        this.containerName = containerName;
        this.logger = logger ?? Log.Logger;
    }

    public async Task<Result<FileInfo>> Build()
    {
        var executableResult = await BuildUtils.GetExecutable(root, setup, logger);
        if (executableResult.IsFailure)
        {
            return Result.Failure<FileInfo>(executableResult.Error);
        }

        var executable = executableResult.Value;

        var architectureResult = await BuildUtils.GetArch(setup, executable);
        if (architectureResult.IsFailure)
        {
            return Result.Failure<FileInfo>(architectureResult.Error);
        }

        var architecture = architectureResult.Value;
        logger.Information("Architecture resolved to {Arch}", architecture);

        var metadata = await BuildUtils.CreateMetadata(setup, root, architecture, executable, setup.IsTerminal, containerName, logger);
        var plan = RpmLayoutBuilder.Build(root, metadata, executable);

        return await RpmPackager.CreatePackage(metadata, plan);
    }
}
