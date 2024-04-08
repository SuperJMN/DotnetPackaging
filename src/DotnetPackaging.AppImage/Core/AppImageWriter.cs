using Zafiro.FileSystem.Lightweight;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;
using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage.Core;

public class AppImageWriter
{
    public static Task<Result> Write(Stream stream, Model.AppImage appImage, ZafiroPath executableFilePath)
    {
        return appImage.Runtime.WriteTo(stream)
            .Bind(() =>
        {
            return WritePayload(stream, appImage.Application, executableFilePath);
        });
    }

    private static Task<Result> WritePayload(Stream stream, Application appImageApplication, ZafiroPath executableFilePath)
    {
        var payload = GetPayload(appImageApplication);
        return SquashFS.Write(stream, payload, executableFilePath);
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