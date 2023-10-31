using System.Reactive.Linq;
using Archiver.Tar;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Serilog;

namespace Archive.Tests;

public class FileModeTests
{
    [Fact]
    public void Test()
    {
        var fileModes = FileMode.Parse("764");
        
        var str = fileModes.ToString();
        str.Should().Be("764");
    }

    [Fact]
    public async Task AnotherTest()
    {
        var properties = new Properties()
        {
            FileMode = FileMode.Parse("777"),
            GroupName = Maybe<string>.None,
            GroupId = Maybe<int>.None,
            LastModification = DateTimeOffset.Now,
            OwnerId = Maybe<int>.None,
            OwnerUsername = Maybe<string>.None
        };

        var entryData = new EntryData("Pepito", properties, () => File.OpenRead("D:\\5 - Unimportant\\Descargas\\recordatorioCita.pdf"));
        var s = new Entry(entryData, Maybe<ILogger>.None);

        using (var memoryStream = File.OpenWrite(@"C:\Users\JMN\Desktop\file.txt"))
        {
            s.Bytes.ToList().Subscribe(list => { });
        }

        //var bytes = await s.Bytes.ToList();
    }
}