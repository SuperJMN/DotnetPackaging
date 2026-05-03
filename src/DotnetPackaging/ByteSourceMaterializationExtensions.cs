using Zafiro.DivineBytes;

namespace DotnetPackaging;

public static class ByteSourceMaterializationExtensions
{
    /// <summary>
    /// Opens a readable stream for APIs that require <see cref="Stream.Length"/>.
    /// </summary>
    /// <remarks>
    /// Known-length sources are adapted without disk I/O. A temporary file is a deliberate fallback only when
    /// length metadata is unavailable; do not force this path by stripping or recomputing <see cref="IByteSource.Length"/>.
    /// </remarks>
    public static async Task<Result<ByteSourceReadLease>> OpenReadWithLength(
        this IByteSource source,
        string extension = "",
        CancellationToken cancellationToken = default)
    {
        var length = source.KnownLength();
        if (length.HasValue)
        {
            return Result.Success(new ByteSourceReadLease(
                new LengthAwareByteSourceStream(source, length.Value),
                length.Value));
        }

        var tempFile = await source.ToTempFile(extension, cancellationToken).ConfigureAwait(false);
        if (tempFile.IsFailure)
        {
            return Result.Failure<ByteSourceReadLease>(tempFile.Error);
        }

        return new ByteSourceReadLease(tempFile.Value.OpenRead(), tempFile.Value.Length, tempFile.Value);
    }

    /// <summary>
    /// Materializes a byte source to a temporary file.
    /// </summary>
    /// <remarks>
    /// This is the expensive escape hatch for length-seeking APIs. Prefer preserving <see cref="IByteSource.Length"/>
    /// on composed byte sources so callers can avoid this method for large payloads.
    /// </remarks>
    public static async Task<Result<MaterializedByteSourceFile>> ToTempFile(
        this IByteSource source,
        string extension = "",
        CancellationToken cancellationToken = default)
    {
        var file = MaterializedByteSourceFile.Create(extension);

        try
        {
            await using var stream = File.Open(file.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var write = await source.WriteTo(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (write.IsFailure)
            {
                file.Dispose();
                return Result.Failure<MaterializedByteSourceFile>(write.Error);
            }

            return file;
        }
        catch (Exception ex)
        {
            file.Dispose();
            return Result.Failure<MaterializedByteSourceFile>(ex.Message);
        }
    }

    public static IByteSource FromTempFileFactory(Func<Task<Result<MaterializedByteSourceFile>>> factory)
    {
        return ByteSource.FromDisposableAsync(factory, file => file.ToByteSource());
    }
}
