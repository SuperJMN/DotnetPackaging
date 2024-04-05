using ClassLibrary1;
using DotnetPackaging.AppImage.Model;
using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage;

public class ApplicationDirectory
{
    public static IDirectory Create(Application application)
    {
        var relativeToItself = application.Contents.RelativeToItself();

        return new CraftedDirectory(directory =>
        {
            IEnumerable<IZafiroFile> files =
            [
                new InMemoryFile("AppRun", directory, application.AppRun),
                new InMemoryFile(".AppIcon", directory, application.Icon),
                new InMemoryFile("App.desktop", directory, GetStream(application.DesktopMetadata))
            ];
            return files;
        }, new[]
        {
            relativeToItself
        });
    }

    private static IGetStream GetStream(DesktopMetadata applicationDesktopMetadata)
    {
        throw new NotImplementedException();
    }
}