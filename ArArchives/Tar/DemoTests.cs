using System.Reactive.Linq;
using Archiver.Tar;
using Zafiro.IO;

namespace Archive.Tests.Tar;

public class DemoTests
{
    [Fact]
    public async Task Demo()
    {
        await using var output = File.Create("C:\\Users\\jmn\\Desktop\\Demo.tar");

        var entry1 = new EntryData("recordatorioCita.pdf", new Properties
        {
            FileMode = FileMode.Parse("644"),
            GroupId = 1000,
            GroupName = "jmn",
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            OwnerUsername = "jmn",
            Length = new FileInfo("D:\\5 - Unimportant\\Descargas\\recordatorioCita.pdf").Length
        }, () => Observable.Using(() => File.OpenRead("D:\\5 - Unimportant\\Descargas\\recordatorioCita.pdf"), stream => stream.ToObservable()));

        var entry2 = new EntryData("wasabi.deb", new Properties
        {
            FileMode = FileMode.Parse("644"),
            GroupId = 1000,
            GroupName = "jmn",
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            OwnerUsername = "jmn",
            Length = new FileInfo("D:\\5 - Unimportant\\Descargas\\Wasabi-2.0.4.deb").Length
        }, () => Observable.Using(() => File.OpenRead("D:\\5 - Unimportant\\Descargas\\Wasabi-2.0.4.deb"), stream => stream.ToObservable()));

        var tarFile = new TarFile(entry1, entry2);


        await tarFile.Bytes.DumpTo(output);
    }
}