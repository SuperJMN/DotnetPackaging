using ClassLibrary1;
using CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace DotnetPackaging.AppImage;

public static class GetStreamMixin
{
    public static async Task<Result> WriteTo(this IGetStream getStream, Stream stream)
    {
        var writeTo = await getStream.StreamFactory()
            .Map(async sourceStream =>
            {
                using (sourceStream)
                {
                    await sourceStream.CopyToAsync(stream);
                }
            });

        return writeTo;
    }

    public static Task<Result<byte[]>> ToBytes(this IGetStream getStream)
    {
        return getStream.StreamFactory()
            .Map(async sourceStream =>
            {
                await using (sourceStream)
                {
                    return await sourceStream.ReadBytes();
                }
            });
    }
}