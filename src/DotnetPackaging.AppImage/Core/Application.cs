using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public class Application
{
    public Application(Metadata metadata, ZafiroPath executablePath, IAppRun appRun, params IDirectory[] contents)
    {
        Contents = contents;
        Metadata = metadata;
        ExecutablePath = executablePath;
        AppRun = appRun;
    }

    public IDirectory[] Contents { get; }
    public IAppRun AppRun { get; }
    public Metadata Metadata { get; }
    public ZafiroPath ExecutablePath { get; }
}