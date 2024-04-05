using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage.Model;

public class Application
{
    public Application(IZafiroDirectory contents, IIcon icon, DesktopMetadata desktopMetadata, IAppRun appRun)
    {
        Contents = contents;
        Icon = icon;
        DesktopMetadata = desktopMetadata;
        AppRun = appRun;
    }

    public IZafiroDirectory Contents { get; }

    private IIcon Icon { get; }
    private IGetStream AppRun { get; }
    public DesktopMetadata DesktopMetadata { get; }
}