using DotnetPackaging.Deb.Archives;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using static DotnetPackaging.Deb.Tests.Ar.Mixin;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.Deb.Tests.Ar;

public class ArFileTests
{
    [Fact]
    public async Task WriteAr()
    {
        var entries = new List<Entry>
        {
            new(StringFile("My entry", "Some content"), DefaultProperties()),
            new(StringFile("Some other entry", "Other content"), DefaultProperties())
        };
        
        var arFile = new ArFile(entries.ToArray());
        var outputStream = new MemoryStream();
        var result = await ArWriter.Write(arFile, outputStream);
        result.Should().Succeed();
        var content = """
                      !<arch>
                      My entry        1579474800  0     0     100777  12        `
                      Some contentSome other entry1579474800  0     0     100777  13        `
                      Other content
                      """.FromCrLfToLf();

        outputStream.ToAscii().Should().Be(content);
    }

    private static Properties DefaultProperties() => new()
    {
        FileMode = UnixFilePermissions.AllPermissions,
        GroupId = 0,
        LastModification = 20.January(2020),
        OwnerId = 0
    };
}