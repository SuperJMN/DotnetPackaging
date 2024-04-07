using ClassLibrary1;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;

namespace DotnetPackaging.AppImage.Core;

public class ApplicationDirectory
{
    public static IBlobContainer Create(Application application)
    {
        var files = GetRootFiles(application);

        return new InMemoryBlobContainer(
            "root",
            files,
            [application.Contents]);
    }

    private static IEnumerable<IBlob> GetRootFiles(Application application)
    {
        IEnumerable<IBlob> mandatory =
        [
            new InMemoryBlob("AppRun", application.AppRun.StreamFactory),
            new InMemoryBlob(".AppIcon", application.Icon.StreamFactory),
        ];

        var optional = application.DesktopMetadata.Match(x =>
            [
                new InMemoryBlob("App.desktop", x.ToStreamFactory())
            ],
            () => new List<IBlob>());

        var files = mandatory.Concat(optional);
        return files;
    }
}