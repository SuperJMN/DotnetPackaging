using System.Text;
using Zafiro.FileSystem.Lightweight;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    [Fact]
    public void Test()
    {
        var builder = new DebFileBuilder()
            .FromDirectory(new SlimDirectory("AvaloniaSyncer", new List<INode>()
            {
                new SlimFile("File1.txt",(StringData)"Content"),
                new SlimFile("File2.txt", (StringData)"Content")
            }))
            .Configure(setup => setup
                .Package("AvaloniaSyncer")
                .PackageId("com.SuperJMN.AvaloniaSyncer")
                .ExecutableName("AvaloniaSyncer.Desktop"))
            .Build();
    }
}