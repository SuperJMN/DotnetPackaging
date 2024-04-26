using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using static DotnetPackaging.Deb.Tests.TestMixin;
using File = System.IO.File;

namespace DotnetPackaging.Deb.Tests;

public class TarFileTests
{
    [Fact]
    public async Task Write_Tar()
    {
        var entries = new List<FileTarEntry>
        {
            new(new RootedFile(ZafiroPath.Empty, 
                StringFile("My entry", "My content")), new TarFileProperties()
            {
                FileMode = UnixFilePermissions.AllPermissions,
                GroupId = 1000,
                GroupName = "group1",
                OwnerUsername = "owner1",
                OwnerId = 1000,
                LastModification = 1.January(2023),
            }),
            new FileTarEntry(new RootedFile(
                ZafiroPath.Empty,
                StringFile("Other entry", "Other content")),  new TarFileProperties()
            {
                FileMode = (UnixFilePermissions)Convert.ToInt32("755", 8),
                GroupId = 123,
                OwnerId = 567,
                GroupName = "group2",
                OwnerUsername = "owner2",
                LastModification = 1.January(2023),
            })
        };

        var tarFile = new TarFile(entries.ToArray());
        var outputStream = new MemoryStream();
        var result = await TarWriter.Write(tarFile, outputStream);
        result.Should().Succeed();
        outputStream.Should().BeEquivalentTo(File.OpenRead("TestFiles/Sample.tar"));
    }
}