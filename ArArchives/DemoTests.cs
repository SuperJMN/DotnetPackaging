using Archiver.Tar;
using CSharpFunctionalExtensions;
using Serilog;

namespace Archive.Tests;

public class DemoTests
{
    [Fact]
    public void Demo()
    {
        var tarFile = new Tar(File.Create("C:\\Users\\jmn\\Desktop\\Demo.tar"), Maybe<ILogger>.None);
        tarFile.Build(
            new EntryData("recordatorioCita.pdf", new Properties()
                {
                    FileModes = FileModes.Parse("644"),
                    GroupId = 1000,
                    GroupName = "jmn",
                    LastModification = DateTimeOffset.Now,
                    OwnerId = 1000,
                    OwnerUsername = "jmn"
                }, File.OpenRead("D:\\5 - Unimportant\\Descargas\\recordatorioCita.pdf")));
    }
}