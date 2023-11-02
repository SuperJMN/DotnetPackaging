using System.Reactive.Linq;
using DotnetPackaging.Deb;
using DotnetPackaging.Tar;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Tar;

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
            Length = new FileInfo("D:\\5 - Unimportant\\Descargas\\recordatorioCita.pdf").Length,
            LinkIndicator = 0,
        }, () => Observable.Using(() => File.OpenRead("D:\\5 - Unimportant\\Descargas\\recordatorioCita.pdf"), stream => stream.ToObservable()));

        var entry2 = new EntryData("wasabi.deb", new Properties
        {
            FileMode = FileMode.Parse("644"),
            GroupId = 1000,
            GroupName = "jmn",
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            OwnerUsername = "jmn",
            Length = new FileInfo("D:\\5 - Unimportant\\Descargas\\Wasabi-2.0.4.deb").Length,
            LinkIndicator = 0,
            
        }, () => Observable.Using(() => File.OpenRead("D:\\5 - Unimportant\\Descargas\\Wasabi-2.0.4.deb"), stream => stream.ToObservable()));

        var tarFile = new TarFile(entry1, entry2);


        await tarFile.Bytes.DumpTo(output);
    }

    [Fact]
    public async Task TarIcon()
    {
        var iconData = new IconData(64, () =>
        {
            return Observable.Using(() => File.OpenRead("Tar\\TestFiles\\icon.png"), stream => stream.ToObservable());
        });

        var properties = new Properties()
        {
            Length = iconData.Bytes().ToEnumerable().Count(),
            FileMode = FileMode.Parse("777"),
            LinkIndicator = 0,
            GroupId = 1000,
            GroupName = "root",
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            OwnerUsername = "root"
        };

        var iconEntry = new EntryData("Icon.png", properties, iconData.Bytes);
        var tarFile = new TarFile(iconEntry);
        await using var output = File.Create("C:\\Users\\jmn\\Desktop\\Testing\\TarIcon.tar");
        await tarFile.Bytes.DumpTo(output);
    }
}