using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Model;

public class Application
{
    public Application(IBlobContainer applicationBlobContainer, Maybe<IIcon> icon, Maybe<DesktopMetadata> desktopMetadata, IAppRun appRun)
    {
        Contents = applicationBlobContainer;
        Icon = icon;
        DesktopMetadata = desktopMetadata;
        AppRun = appRun;
    }

    public IBlobContainer Contents { get; }
    public Maybe<IIcon> Icon { get; }
    public IAppRun AppRun { get; }
    public Maybe<DesktopMetadata> DesktopMetadata { get; }
}