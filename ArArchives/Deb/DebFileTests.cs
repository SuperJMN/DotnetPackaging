using System.Reactive.Linq;
using Archiver.Deb;
using Zafiro.IO;

namespace Archive.Tests.Deb;

public class DebFileTests
{
    [Fact]
    public async Task Test()
    {
        var debFile = new DebFile(new Metadata()
        {
            PackageName = "AvaloniaSynchronizer",
            ApplicationName = "AvaloniaSynchronizer",
            Architecture = "amd64",
            Homepage = "www.blablabla.com",
            License = "MIT",
            Maintainer = "SuperJMN"
        });

        await debFile.Bytes.DumpTo(File.Create("C:\\Users\\JMN\\Desktop\\Demo.deb"));
    }
}