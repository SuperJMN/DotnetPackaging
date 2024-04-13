using Zafiro.FileSystem.Lightweight;
using CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace DotnetPackaging.AppImage.Core;

public static class GetStreamMixin
{
    public static async Task<Result> WriteTo(this IStreamOpen streamOpen, Stream stream)
    {
        var writeTo = await streamOpen.Open()
            .Map(async sourceStream =>
            {
                using (sourceStream)
                {
                    await sourceStream.CopyToAsync(stream);
                }
            });

        return writeTo;
    }

    public static Task<Result<byte[]>> ToBytes(this IStreamOpen streamOpen)
    {
        return streamOpen.Open()
            .Map(async sourceStream =>
            {
                await using (sourceStream)
                {
                    return await sourceStream.ReadBytes();
                }
            });
    }
    
    public static Task<Result<T>> Within<T>(this IStreamOpen streamOpen, Func<Stream, Task<Result<T>>> apply)
    {
        return streamOpen.Open()
            .Bind(async sourceStream =>
            {
                await using (sourceStream)
                {
                    return await apply(sourceStream);
                }
            });
    }
    
    public static Task<Result<T>> Within<T>(this IStreamOpen streamOpen, Func<Stream, Result<T>> apply)
    {
        return streamOpen.Open()
            .Bind(async sourceStream =>
            {
                await using (sourceStream)
                {
                    return apply(sourceStream);
                }
            });
    }
}