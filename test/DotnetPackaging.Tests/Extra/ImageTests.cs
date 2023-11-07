using System.Reactive.Linq;
using DotnetPackaging.Common;
using DotnetPackaging.Deb;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Extra;

public class ImageTests
{
    [Fact]
    public async Task Image_test()
    {
        var iconData = new IconData(32, new FileInfo("TestFiles\\icon.png").ToByteStore());

        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\Testing\\resizedicon.png");
        await iconData.TargetedBytes.DumpTo(output);
    }
}