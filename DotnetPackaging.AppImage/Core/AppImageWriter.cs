using ClassLibrary1;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;

namespace DotnetPackaging.AppImage.Core;

public class AppImageWriter
{
    public static Task<Result> Write(Stream stream, Model.AppImage appImage)
    {
        return appImage.Runtime.WriteTo(stream)
            .Bind(() =>
        {
            return WritePayload(stream, appImage.Application);
        });
    }

    private static Task<Result> WritePayload(Stream stream, Application appImageApplication)
    {
        var payload = GetPayload(appImageApplication);
        return SquashFS.Write(stream, payload);
    }

    private static IBlobContainer GetPayload(Application application)
    {
        var root = new BlobContainer(new List<IBlob>()
        {
            new Blob("AppRun", application.AppRun.StreamFactory),
            new Blob(".AppIcon", application.Icon.StreamFactory),
        }, new List<IBlobContainer>()
        {
            application.Contents
        });

        return root;
    }
}