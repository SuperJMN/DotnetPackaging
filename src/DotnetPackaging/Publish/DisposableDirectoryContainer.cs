using System.IO.Abstractions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;

namespace DotnetPackaging.Publish;

public sealed class DisposableDirectoryContainer : IDisposableContainer
{
    private readonly Lazy<RootContainer> container;
    private readonly ILogger logger;
    private bool disposed;

    public DisposableDirectoryContainer(string outputDirectory, ILogger logger)
    {
        this.OutputPath = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
        this.logger = logger;
        container = new Lazy<RootContainer>(CreateContainer);
    }

    public string OutputPath { get; }

    public IEnumerable<INamedContainer> Subcontainers => container.Value.Subcontainers;

    public IEnumerable<INamedByteSource> Resources => container.Value.Resources;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        try
        {
            if (Directory.Exists(OutputPath))
            {
                logger.Debug("Deleting publish directory {Directory}", OutputPath);
                Directory.Delete(OutputPath, true);
                logger.Debug("Deleted publish directory {Directory}", OutputPath);
            }
        }
        catch (Exception ex)
        {
            logger.Warning("Failed to delete publish directory {Directory}: {Error}", OutputPath, ex.Message);
        }
    }

    private RootContainer CreateContainer()
    {
        var fs = new FileSystem();
        var directoryInfo = fs.DirectoryInfo.New(OutputPath);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Publish directory '{OutputPath}' does not exist");
        }

        return new DirectoryContainer(directoryInfo).AsRoot();
    }
}
