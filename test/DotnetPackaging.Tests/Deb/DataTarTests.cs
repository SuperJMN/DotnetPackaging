using System.Reactive.Linq;
using DotnetPackaging.Deb;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Deb;

public class DataTarTests
{
    [Fact]
    public async Task DataTar()
    {
        var contents = TestData.Contents();
        var dataTar = new DataTar(TestData.Metadata(), contents);

        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\Testing\\data.tar");
        await dataTar.Tar.Bytes.DumpTo(output);
    }
}