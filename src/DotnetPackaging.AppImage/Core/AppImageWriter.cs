using Zafiro.FileSystem.Lightweight;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;
using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage.Core;

public class AppImageWriter
{
    public static Task<Result> Write(Stream stream, Model.AppImage appImage)
    {
        return appImage.Runtime.WriteTo(stream)
            .Bind(() => WritePayload(stream, appImage.Application));
    }

    private static Task<Result> WritePayload(Stream stream, Application appImageApplication)
    {
        return GetPayload(appImageApplication)
            .ToLinuxFileEntries()
            .Bind(entries => SquashFS.Write(stream, entries));
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

public record LinuxFileEntry(ZafiroPath path, IGetStream data, string owner, string group, UnixFileMode unixFileMode);