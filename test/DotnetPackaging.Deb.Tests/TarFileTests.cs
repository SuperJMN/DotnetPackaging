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
            new(StringFile("My entry", "Some content"), new TarProperties()
            {
                FileMode = UnixFilePermissions.AllPermissions,
                GroupId = 1,
                GroupName = "group1",
                OwnerUsername = "owner1",
                LinkIndicator = 1,
                OwnerId = 1,
                LastModification = 20.January(2020),
            }),
            //new(StringFile("My content", "Other content"), new TarProperties()
            //{
            //    FileMode = UnixFilePermissions.AllPermissions,
            //    GroupId = 1,
            //    GroupName = "group2",
            //    OwnerUsername = "owner2",
            //    LinkIndicator = 1,
            //    OwnerId = 0,
            //    LastModification = 20.January(2020),
            //})
        };

        var tarFile = new TarFile(entries.ToArray());
        var outputStream = new MemoryStream();
        var result = await TarWriter.Write(tarFile, outputStream);
        result.Should().Succeed();
        outputStream.WriteTo(File.OpenWrite("C:\\Users\\JMN\\Desktop\\actual.tar"));
        outputStream.ToArray().ShouldBeEquivalentToWithBinaryFormat(await File.ReadAllBytesAsync("TestFiles/Sample.tar"));
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