using System.IO.Abstractions;
using ClassLibrary1;
using Zafiro.FileSystem.Local;

namespace DotnetPackaging.AppImage.Tests;

public class UnitTest1
{
    [Fact]
    public async Task SquashFS()
    {
        var fs = new FileSystem();
        var output = File.OpenWrite("\\\\wsl.localhost\\Ubuntu\\home\\jmn\\test.squashfs");
        var bc = new DirectoryBlobContainer("AvaloniaSyncer", fs.DirectoryInfo.New(@"C:\Users\JMN\Desktop\Testing"));
        await Core.SquashFS.Write(output, bc);
    }
}