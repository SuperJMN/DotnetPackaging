using Archiver.Tar;
using CSharpFunctionalExtensions;
using Serilog;

namespace Archive.Tests;

public class DemoTests
{
    [Fact]
    public async Task Demo()
    {
        await using var fileStream = File.Create("C:\\Users\\jmn\\Desktop\\Demo.tar");
        var tarFile = new Tar(fileStream, Maybe<ILogger>.None);

        var entry1 = new EntryData("recordatorioCita.pdf", new Properties
        {
            FileModes = FileModes.Parse("644"),
            GroupId = 1000,
            GroupName = "jmn",
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            OwnerUsername = "jmn"
        }, () => File.OpenRead("D:\\5 - Unimportant\\Descargas\\recordatorioCita.pdf"));

        var entry2 = new EntryData("wasabi.deb", new Properties
        {
            FileModes = FileModes.Parse("644"),
            GroupId = 1000,
            GroupName = "jmn",
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            OwnerUsername = "jmn"
        }, () => File.OpenRead("D:\\5 - Unimportant\\Descargas\\Wasabi-2.0.4.deb"));


        await tarFile.Build(entry1, entry2);
    }
}