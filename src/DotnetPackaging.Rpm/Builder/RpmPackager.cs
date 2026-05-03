using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Rpm.Builder;

internal static class RpmPackager
{
    public static async Task<Result<IByteSource>> CreatePackage(PackageMetadata metadata, RpmLayout layout)
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
            var bytes = await RpmArchiveWriter.Build(metadata, layout).ConfigureAwait(false);
            return bytes.Map(data => ByteSource.FromBytes(data).WithLength(data.LongLength));
        }
        catch (Exception ex)
        {
            return Result.Failure<IByteSource>(ex.Message);
        }
    }
}
