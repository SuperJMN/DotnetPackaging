using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;
using System.IO;

namespace DotnetPackaging.AppImage;

public class AppImageWriter
{
    public async Task<Result> Write(Stream stream, AppImage.Model.AppImage appImage)
    {
        return await appImage.Runtime.WriteTo(stream)
            .Bind(() => WriteApplication(appImage.Application, stream));
    }

    private async Task<Result> WriteApplication(Application application, Stream stream)
    {
        return await SquashFS.Build(new ApplicationDirectory(application))
            .Map(async stream1 =>
            {
                using (stream1)
                {
                    await stream1.CopyToAsync(stream);
                }
            });
    }
}

public static class Mixin
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
}