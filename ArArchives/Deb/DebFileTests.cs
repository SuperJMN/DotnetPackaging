using System.Reactive.Linq;
using Archiver;
using Archiver.Deb;
using Zafiro.FileSystem;
using Zafiro.IO;
using DebFile = Archiver.Deb.DebFile;

namespace Archive.Tests.Deb;

public class DebFileTests
{
    [Fact]
    public async Task Test()
    {
        var contents = new Contents(new Dictionary<ZafiroPath, Func<IObservable<byte>>>()
        {
            ["Contenido1.txt"] = () => "Soy pepito".GetAsciiBytes().ToObservable(),
            ["Contenido2.txt"] = () => "Dale, Don, dale.".GetAsciiBytes().ToObservable()
        });

        var debFile = new DebFile(new Metadata()
        {
            PackageName = "AvaloniaSynchronizer",
            ApplicationName = "AvaloniaSynchronizer",
            Architecture = "amd64",
            Homepage = "www.blablabla.com",
            License = "MIT",
            Maintainer = "SuperJMN"
        }, contents);


        await debFile.Bytes.DumpTo(File.Create("C:\\Users\\JMN\\Desktop\\Demo.deb"));
    }
}