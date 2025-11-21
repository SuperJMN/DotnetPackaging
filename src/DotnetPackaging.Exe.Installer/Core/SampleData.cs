using Avalonia.Platform;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.Installer.Core;

public class SampleData
{
    public static IByteSource Logo()
    {
        return ByteSource.FromStreamFactory(() => AssetLoader.Open(new Uri("avares://DotnetPackaging.Exe.Installer/Assets/icon.png")));
    }
}