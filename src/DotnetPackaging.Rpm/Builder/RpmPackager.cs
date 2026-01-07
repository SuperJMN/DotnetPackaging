using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Rpm.Builder;

internal static class RpmPackager
{
    public static Task<Result<IByteSource>> CreatePackage(PackageMetadata metadata, RpmLayout layout)
    {
        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        if (layout == null)
        {
            throw new ArgumentNullException(nameof(layout));
        }

        try
        {
            var bytes = RpmArchiveWriter.Build(metadata, layout);
            return Task.FromResult(Result.Success<IByteSource>(ByteSource.FromBytes(bytes)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<IByteSource>(ex.Message));
        }
    }
}
