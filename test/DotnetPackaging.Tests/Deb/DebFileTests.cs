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
    public async Task WriteControlTar()
    {
        var debFile = DebFile();

        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\Testing\\control.tar");
        await debFile.ControlTar().Bytes.DumpTo(output);
    }

    private static DebFile DebFile()
    {
        var iconResources = IconResources.Create(new IconData(64, () => Observable.Using(() => File.OpenRead("TestFiles\\icon.png"), stream => stream.ToObservable())));

        var contents = new Contents
        {
            new RegularContent("Contenido1.txt", () => "Soy pepito".GetAsciiBytes().ToObservable()),
            new RegularContent("Contenido2.txt", () => "Dale, Don, dale.".GetAsciiBytes().ToObservable()),
            new ExecutableContent("Contenido.Desktop", () => "Dale, Don, dale.".GetAsciiBytes().ToObservable(), iconResources.Value),
        };
        
        var debFile = new DebFile(new Metadata
        {
            PackageName = "AvaloniaSynchronizer",
            ApplicationName = "AvaloniaSynchronizer",
            Architecture = "amd64",
            Homepage = "www.blablabla.com",
            License = "MIT",
            Maintainer = "SuperJMN",
            Description = "The file manager you always wanted to have"
        }, contents, iconResources.Value);

        return debFile;
    }
}