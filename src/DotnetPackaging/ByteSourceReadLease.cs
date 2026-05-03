namespace DotnetPackaging;

public sealed class ByteSourceReadLease : IDisposable
{
    private readonly MaterializedByteSourceFile? file;
    private bool disposed;

    public ByteSourceReadLease(Stream stream, long length, MaterializedByteSourceFile? file = null)
    {
        Stream = stream;
        Length = length;
        this.file = file;
    }

    public Stream Stream { get; }
    public long Length { get; }
    public bool UsesTemporaryFile => file is not null;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Stream.Dispose();
        file?.Dispose();
    }
}
