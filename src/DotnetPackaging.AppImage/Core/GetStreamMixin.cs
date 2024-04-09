using Zafiro.FileSystem.Lightweight;
using CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace DotnetPackaging.AppImage.Core;

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
    
    public static Task<Result<T>> Within<T>(this IGetStream getStream, Func<Stream, Task<Result<T>>> apply)
    {
        return getStream.StreamFactory()
            .Bind(async sourceStream =>
            {
                await using (sourceStream)
                {
                    return await apply(sourceStream);
                }
            });
    }
    
    public static Task<Result<T>> Within<T>(this IGetStream getStream, Func<Stream, Result<T>> apply)
    {
        return getStream.StreamFactory()
            .Bind(async sourceStream =>
            {
                await using (sourceStream)
                {
                    return apply(sourceStream);
                }
            });
    }
}