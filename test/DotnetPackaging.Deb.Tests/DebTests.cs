using System.Reactive.Linq;
using System.Text;
using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions;
using FluentAssertions.Common;
using FluentAssertions.Extensions;
using Xunit;
using Zafiro.FileSystem.Lightweight;
using IoFile = System.IO.File;

namespace DotnetPackaging.Deb.Tests;

public class DebTests
{
    [Fact]
    public async Task Deb_test()
    {
        var metadata = new Metadata
        {
            Package = "test",
            Version = "1.0-1",
            Section = "utils",
            Priority = "optional",
            Architecture = "all",
            Maintainer = "Baeldung <test@test.com>",
            Description = "This is a test application\n for packaging",
            ModificationTime = 25.April(2024).AddHours(9).AddMinutes(47).AddSeconds(22).ToDateTimeOffset(),
        };

        var shContents = """
                         #!/bin/bash
                         . test.conf
                         echo "Hi $NAME"
                         
                         """.FromCrLfToLf();
        
        var confContents = "NAME=Test\n".FromCrLfToLf();

        var defaultFileProperties = new TarFileProperties()
        {
            FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("777"),
            GroupId = 0,
            GroupName = "root",
            LastModification = DateTimeOffset.Now,
            OwnerId = 0,
            OwnerUsername = "root"
        };

        var defaultDirProperties = new TarDirectoryProperties()
        {
            FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("755"),
            GroupId = 0,
            GroupName = "root",
            LastModification = 25.April(2024).AddHours(9).AddMinutes(47).AddSeconds(22).ToDateTimeOffset(),
            OwnerId = 0,
            OwnerUsername = "root",
        };
        
        var tarEntries = new TarEntry[]
        {
            new DirectoryTarEntry("./", defaultDirProperties with { LastModification = DateTimeOffset.Parse("24/04/2024 12:11:05 +00:00") }),
            new DirectoryTarEntry("./bin/", defaultDirProperties with { LastModification = DateTimeOffset.Parse("24/04/2024 12:10:16 +00:00") }),
            new FileTarEntry("./bin/test.sh", new StringByteProvider(shContents, Encoding.ASCII), defaultFileProperties with{  LastModification = DateTimeOffset.Parse("24/04/2024 12:09:08 +00:00")}),
            new DirectoryTarEntry("./etc/", defaultDirProperties with { LastModification = DateTimeOffset.Parse("24/04/2024 12:10:10 +00:00") }),
            new FileTarEntry("./etc/test.conf", new StringByteProvider(confContents, Encoding.ASCII), defaultFileProperties with
            {
                FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("644"),
                LastModification = DateTimeOffset.Parse("24/04/2024 12:06:22 +00:00")
            }),
        };

        var deb = new DebFile(metadata, tarEntries);
        var actual = deb.ToByteProvider().Bytes.Flatten().ToEnumerable().ToArray();
        await IoFile.WriteAllBytesAsync(@"C:\Users\JMN\Desktop\actual.deb", actual);
        var expected = await IoFile.ReadAllBytesAsync("TestFiles/Sample.deb");

        actual.Should().BeEquivalentTo(expected);
    }
}