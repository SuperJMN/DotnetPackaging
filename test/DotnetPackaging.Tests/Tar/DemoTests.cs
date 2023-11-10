using System.Reactive.Linq;
using DotnetPackaging.Deb;
using DotnetPackaging.Tar;
using FluentAssertions.Extensions;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Tar;

public class DemoTests
{
    [Fact]
    public async Task Demo()
    {
        await using var output = File.Create("C:\\Users\\jmn\\Desktop\\Demo.tar");

        var entry1 = new EntryData("icon.png", new Properties
        {
            FileMode = FileMode.Parse("644"),
            GroupId = 1000,
            GroupName = "jmn",
            LastModification = 30.January(1981),
            OwnerId = 1000,
            OwnerUsername = "jmn",
            Length = new FileInfo("TestFiles\\icon.png").Length,
            LinkIndicator = 0,
        }, () => Observable.Using(() => File.OpenRead("TestFiles\\icon.png"), stream => stream.ToObservable()));

        var entry2 = new EntryData("Hello.txt", new Properties
        {
            FileMode = FileMode.Parse("644"),
            GroupId = 1000,
            GroupName = "jmn",
            LastModification = 30.January(1981),
            OwnerId = 1000,
            OwnerUsername = "jmn",
            Length = new FileInfo("TestFiles\\Hello.txt").Length,
            LinkIndicator = 0,
            
        }, () => Observable.Using(() => File.OpenRead("TestFiles\\Hello.txt"), stream => stream.ToObservable()));

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
            Length = iconData.IconBytes().ToEnumerable().Count(),
            FileMode = FileMode.Parse("777"),
            LinkIndicator = 0,
            GroupId = 1000,
            GroupName = "root",
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            OwnerUsername = "root"
        };

        var iconEntry = new EntryData("Icon.png", properties, iconData.IconBytes);
        var entry = new Entry(iconEntry);
        await using var output = File.Create("C:\\Users\\jmn\\Desktop\\Testing\\TarIcon.tar");
        
        await entry.Bytes.DumpTo(output);
    }
}