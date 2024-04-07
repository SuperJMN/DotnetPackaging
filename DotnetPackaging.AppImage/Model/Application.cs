using ClassLibrary1;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Model;

public class Application
{
    public Application(IBlobContainer applicationBlobContainer, IIcon icon, Maybe<DesktopMetadata> desktopMetadata, IAppRun appRun)
    {
        Contents = applicationBlobContainer;
        Icon = icon;
        DesktopMetadata = desktopMetadata;
        AppRun = appRun;
    }

    public IBlobContainer Contents { get; }
    public IIcon Icon { get; }
    public IAppRun AppRun { get; }
    public Maybe<DesktopMetadata> DesktopMetadata { get; }
}