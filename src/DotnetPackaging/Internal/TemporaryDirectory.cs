using System.IO;
using Serilog;

namespace DotnetPackaging.Internal;

public sealed class TemporaryDirectory : IDisposable
{
    private readonly string path;
    private readonly ILogger logger;

    public TemporaryDirectory(string path, ILogger logger)
    {
        this.path = path;
        this.logger = logger;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                logger.Debug("Deleted temporary directory {Path}", path);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to delete temporary directory {Path}", path);
        }
    }

    public static implicit operator string(TemporaryDirectory directory) => directory.path;
    public override string ToString() => path;
}
