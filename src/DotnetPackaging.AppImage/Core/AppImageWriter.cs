using Zafiro.FileSystem.Lightweight;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;

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
        var mandatory = new List<IBlob>()
        {
            new Blob("AppRun", application.AppRun.StreamFactory),
        };
        var optional = application.Icon.Map(icon => (IEnumerable<IBlob>)new[] { new Blob("AppIcon.png", icon.StreamFactory) }).GetValueOrDefault(Enumerable.Empty<IBlob>());
        var files = mandatory.Concat(optional);

        var containers = new List<IBlobContainer>()
        {
            application.Contents
        };

        return new BlobContainer("", files, containers);
    }
}