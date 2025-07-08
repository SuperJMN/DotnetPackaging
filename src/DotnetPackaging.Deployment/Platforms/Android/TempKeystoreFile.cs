namespace DotnetPackaging.Deployment.Platforms.Android;

internal class TempKeystoreFile(string filePath) : IDisposable
{
    public string FilePath { get; } = filePath;
    private bool disposed = false;

    public void Dispose()
    {
        if (!disposed)
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch
            {
                // Log error si es necesario, pero no lanzar excepción en Dispose
            }

            disposed = true;
        }
    }
}