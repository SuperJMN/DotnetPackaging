using System.Reactive.Linq;
using DotnetPackaging.Common;
using DotnetPackaging.Deb;
using Zafiro.FileSystem;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Deb;

public class DataTarTests
{
    [Fact]
    public async Task DataTar()
    {
        var resources = IconResources.Create(new IconData(32, () => Observable.Using(() => File.OpenRead("TestFiles\\icon.png"), stream => stream.ToObservable())));

        var contents = new Contents 
        {
            new RegularContent("Contenido1.txt", () => "Soy pepito".GetAsciiBytes().ToObservable()),
            new RegularContent("Contenido2.txt", () => "Dale, Don, dale.".GetAsciiBytes().ToObservable()),
            new ExecutableContent("Program.Desktop", () => "Binary data. Irrelevant for the test.".GetAsciiBytes().ToObservable(), resources.Value),
        };
        
        var dataTar = new DataTar(new Metadata()
        {
            PackageName = "SamplePackage",
            Description = "This is a sample",
            ApplicationName = "My application",
            Architecture = "amd64",
            Homepage = "https://www.something.com",
            License = "MIT",
            Maintainer = "Me"
        }, resources.Value, contents);

        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\Testing\\data.tar");
        await dataTar.Tar.Bytes.DumpTo(output);
    }
}