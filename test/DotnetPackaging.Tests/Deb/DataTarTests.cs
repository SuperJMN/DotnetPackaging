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

        var contents = new Contents(new Dictionary<ZafiroPath, Content>
        {
            ["Contenido1.txt"] = new RegularContent(() => "Soy pepito".GetAsciiBytes().ToObservable()),
            ["Contenido2.txt"] = new RegularContent(() => "Dale, Don, dale.".GetAsciiBytes().ToObservable()),
            ["Program.Desktop"] = new ExecutableContent(() => "Binary data. Irrelevant for the test.".GetAsciiBytes().ToObservable(), resources.Value),
        });

        
        var dataTar = new DataTar("Test", resources.Value, contents);
        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\Testing\\data.tar");
        await dataTar.Tar.Bytes.DumpTo(output);
    }
}