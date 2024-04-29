using System.Reactive.Linq;
using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions;
using FluentAssertions.Common;
using FluentAssertions.Extensions;
using Xunit;
using IoFile = System.IO.File;

namespace DotnetPackaging.Deb.Tests;

public class DebTests
{
    [Fact]
    public async Task Deb_test()
    {
        var dateTimeOffset = 24.April(2024).AddHours(14).AddMinutes(11).AddSeconds(5).ToDateTimeOffset();
        
        var deb = new DebFile(new Metadata
            {
                Package = "test",
                Version = "1.0-1",
                Section = "utils",
                Priority = "optional",
                Architecture = "all",
                Maintainer = "Baeldung <test@test.com>",
                Description = "This is a test application\n for packaging",
                ModificationTime = 25.April(2024).AddHours(9).AddMinutes(47).AddSeconds(22).ToDateTimeOffset(),
            }, new DirectoryTarEntry("", new TarDirectoryProperties()
        {
            FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("755"),
            GroupId = 0,
            GroupName = "root",
            LastModification = 25.April(2024).AddHours(9).AddMinutes(47).AddSeconds(22).ToDateTimeOffset(),
            OwnerId = 0,
            OwnerUsername = "root",
        }));

        var actual = deb.ToByteProvider().Bytes.Flatten().ToEnumerable().ToArray();
        await IoFile.WriteAllBytesAsync(@"C:\Users\JMN\Desktop\actual.deb", actual);
        var expected = await IoFile.ReadAllBytesAsync("TestFiles/Sample.deb");
        
        //new FileEntry(
        //    new RootedFile("etc", new File("etc", String("NAME=Test"))), new UnixFileProperties()
        //    {
        //        FileMode = UnixFilePermissions.Read | UnixFilePermissions.Write | UnixFilePermissions.GroupRead | UnixFilePermissions.OtherRead,
        //        GroupId = 0,
        //        OwnerUsername = "jmn",
        //        GroupName = "jmn",
        //        OwnerId = 0,
        //        LastModification = dateTimeOffset
        //    })

        actual.Should().BeEquivalentTo(expected);
    }
}