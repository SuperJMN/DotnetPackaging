using DotnetPackaging.AppImage.Tests;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.AppImage;

public class AppImage
{
    public IRuntime Runtime { get; }
    public UnixRoot Root { get; }

    public AppImage(IRuntime runtime, UnixRoot root)
    {
        Runtime = runtime;
        Root = root;
    }
}