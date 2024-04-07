using ClassLibrary1;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Model;

public class Application
{
    public Application(IDataTree contents, IIcon icon, Maybe<DesktopMetadata> desktopMetadata, IAppRun appRun)
    {
        Contents = contents;
        Icon = icon;
        DesktopMetadata = desktopMetadata;
        AppRun = appRun;
    }

    public IDataTree Contents { get; }
    public IIcon Icon { get; }
    public IAppRun AppRun { get; }
    public Maybe<DesktopMetadata> DesktopMetadata { get; }
}