using System.Reactive.Linq;
using DotnetPackaging.Deb;
using Zafiro.FileSystem;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Deb;

public class DebFileTests
{
    [Fact]
    public async Task FullDebTest()
    {
        var contents = new Contents(new Dictionary<ZafiroPath, Func<IObservable<byte>>>()
        {
            ["Contenido1.txt"] = () => "Soy pepito".GetAsciiBytes().ToObservable(),
            ["Contenido2.txt"] = () => "Dale, Don, dale.".GetAsciiBytes().ToObservable()
        });

        var debFile = new DotnetPackaging.Deb.DebFile(new Metadata()
        {
            PackageName = "AvaloniaSynchronizer",
            ApplicationName = "AvaloniaSynchronizer",
            Architecture = "amd64",
            Homepage = "http://www.superjmn.com",
            License = "MIT",
            Maintainer = "SuperJMN"
        }, contents);


        await using var fileStream = File.OpenWrite("C:\\Users\\JMN\\Desktop\\Testing\\FullDebTest.deb");
        await debFile.Bytes.DumpTo(fileStream);
    }

    [Fact]
    public async Task WriteDataTar()
    {
        var contents = new Contents(new Dictionary<ZafiroPath, Func<IObservable<byte>>>()
        {
            ["Contenido1.txt"] = () => "Soy pepito".GetAsciiBytes().ToObservable(),
            ["Contenido2.txt"] = () => "Dale, Don, dale.".GetAsciiBytes().ToObservable()
        });

        var debFile = new DotnetPackaging.Deb.DebFile(new Metadata()
        {
            PackageName = "AvaloniaSynchronizer",
            ApplicationName = "AvaloniaSynchronizer",
            Architecture = "amd64",
            Homepage = "www.blablabla.com",
            License = "MIT",
            Maintainer = "SuperJMN"
        }, contents);

        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\data.tar");
        await debFile.DataTar().Bytes.DumpTo(output);
    }

    [Fact]
    public async Task WriteControlTar()
    {
        var contents = new Contents(new Dictionary<ZafiroPath, Func<IObservable<byte>>>()
        {
            ["Contenido1.txt"] = () => "Soy pepito".GetAsciiBytes().ToObservable(),
            ["Contenido2.txt"] = () => "Dale, Don, dale.".GetAsciiBytes().ToObservable()
        });

        var debFile = new DotnetPackaging.Deb.DebFile(new Metadata()
        {
            PackageName = "AvaloniaSynchronizer",
            ApplicationName = "AvaloniaSynchronizer",
            Architecture = "amd64",
            Homepage = "www.blablabla.com",
            License = "MIT",
            Maintainer = "SuperJMN"
        }, contents);

        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\control.tar");
        await debFile.ControlTar().Bytes.DumpTo(output);
    }
}