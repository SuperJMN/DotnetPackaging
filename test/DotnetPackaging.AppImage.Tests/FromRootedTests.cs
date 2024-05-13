using Zafiro.DataModel;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.AppImage.Tests;

public class FromRootedTests
{
    [Fact]
    public void FromRooted()
    {
        var files = new[]
        {
            new RootedUnixFile(ZafiroPath.Empty, new UnixFile("Sample1.txt", (StringData) "Content")),
            new RootedUnixFile(ZafiroPath.Empty, new UnixFile("Sample2.txt", (StringData)"Content")),
            new RootedUnixFile("Dir", new UnixFile("Sample3.txt", (StringData)"Content")),
            new RootedUnixFile("Dir/Subdir", new UnixFile("Sample4.txt", (StringData)"Content")),
        };

        var root = files.FromRootedFiles(ZafiroPath.Empty);
    }
}