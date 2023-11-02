using System.Reactive.Linq;
using DotnetPackaging.Common;
using DotnetPackaging.Deb;
using Zafiro.FileSystem;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Deb;

public class DebFileTests
{
    [Fact]
    public async Task FullDebTest()
    {
        var debFile = DebFile();

        await using var fileStream = File.Create("C:\\Users\\JMN\\Desktop\\Testing\\FullDebTest.deb");
        await debFile.Bytes.DumpTo(fileStream);
    }

    [Fact]
    public async Task WriteDataTar()
    {
        var debFile = DebFile();

        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\data.tar");
        await debFile.DataTar().Bytes.DumpTo(output);
    }

    [Fact]
    public async Task WriteControlTar()
    {
        var debFile = DebFile();

        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\control.tar");
        await debFile.ControlTar().Bytes.DumpTo(output);
    }

    private static DebFile DebFile()
    {
        var contents = new Contents(new Dictionary<ZafiroPath, Content>
        {
            ["Contenido1.txt"] = new Content(() => "Soy pepito".GetAsciiBytes().ToObservable()),
            ["Contenido2.txt"] = new Content(() => "Dale, Don, dale.".GetAsciiBytes().ToObservable())
        });

        var debFile = new DebFile(new Metadata
        {
            PackageName = "AvaloniaSynchronizer",
            ApplicationName = "AvaloniaSynchronizer",
            Architecture = "amd64",
            Homepage = "www.blablabla.com",
            License = "MIT",
            Maintainer = "SuperJMN",
            Description = "The file manager you always wanted to have"
        }, contents);
        return debFile;
    }
}