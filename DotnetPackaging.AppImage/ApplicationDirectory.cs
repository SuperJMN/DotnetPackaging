using ClassLibrary1;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;

namespace DotnetPackaging.AppImage;

public class ApplicationDirectory
{
    public static IDataTree Create(Application application)
    {
        var files = GetRootFiles(application);

        return new InMemoryDataTree(
            "root",
            files,
            [application.Contents]);
    }

    private static IEnumerable<IData> GetRootFiles(Application application)
    {
        IEnumerable<IData> mandatory =
        [
            new InMemoryData("AppRun", application.AppRun.StreamFactory),
            new InMemoryData(".AppIcon", application.Icon.StreamFactory),
        ];

        var optional = application.DesktopMetadata.Match(x =>
            [
                new InMemoryData("App.desktop", x.ToStreamFactory())
            ],
            () => new List<IData>());

        var files = mandatory.Concat(optional);
        return files;
    }
}