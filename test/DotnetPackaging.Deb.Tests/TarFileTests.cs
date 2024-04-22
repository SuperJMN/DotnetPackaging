using DotnetPackaging.Deb.Archives.Ar;
using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using static DotnetPackaging.Deb.Tests.Ar.Mixin;

namespace DotnetPackaging.Deb.Tests;

public class TarFileTests
{
    [Fact]
    public async Task Write_Tar()
    {
        var entries = new List<TarEntry>
        {
            new(StringFile("My entry", "Some content"), DefaultProperties()),
            new(StringFile("Some other entry", "Other content"), DefaultProperties())
        };

        //var arFile = new TarFile(entries.ToArray());
        var outputStream = new MemoryStream();
        //var result = await TarWriter.Write(arFile, outputStream);
        //result.Should().Succeed();
        outputStream.ToAscii().Should().Be(await File.ReadAllTextAsync("TestFiles/Sample.tar"));
    }

    private TarProperties DefaultProperties()
    {
        return new TarProperties()
        {
            FileMode = UnixFilePermissions.AllPermissions,
            GroupId = 1,
            GroupName = "group",
            OwnerUsername = "owner",
            LinkIndicator = 1,
            OwnerId = 0,
            LastModification = 20.January(2020),
        };
    }
}