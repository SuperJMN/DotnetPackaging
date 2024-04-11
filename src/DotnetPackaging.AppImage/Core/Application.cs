using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public class Application
{
    public Application(IBlobContainer[] contents, Maybe<IIcon> icon, Maybe<DesktopMetadata> desktopMetadata, IAppRun appRun)
    {
        Contents = contents;
        Icon = icon;
        DesktopMetadata = desktopMetadata;
        AppRun = appRun;
    }

    public IBlobContainer[] Contents { get; }
    public Maybe<IIcon> Icon { get; }
    public IAppRun AppRun { get; }
    public Maybe<DesktopMetadata> DesktopMetadata { get; }
}