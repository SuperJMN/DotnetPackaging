using DotnetPackaging.AppImage.Kernel;
using Zafiro.DataModel;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.AppImage.Tests;

public class SquahsFSTests
{
    [Fact]
    public async Task Create_SquashFS()
    {
        var root = new UnixRoot(new List<UnixNode>()
        {
            new UnixFile("My file", (StringData)"Content1"),
            new UnixDir("My dir", new List<UnixNode>()
            {
                new UnixFile("Another file.txt", (StringData)"Content2"),
                new UnixFile("One more.txt", (StringData)"Content3"),
            } )
        });

        await SquashFS.Create(root)
            .Bind(data => data.DumpTo("C:\\Users\\JMN\\Desktop\\File.squashfs"));
    }
}