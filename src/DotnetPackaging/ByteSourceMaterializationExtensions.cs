using Zafiro.DivineBytes;

namespace DotnetPackaging;

public static class ByteSourceMaterializationExtensions
{
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
