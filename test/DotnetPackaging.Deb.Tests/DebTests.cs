using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions.Extensions;
using Xunit;
using Zafiro.FileSystem.Lightweight;
using static DotnetPackaging.Deb.Tests.Mixin;
using File = Zafiro.FileSystem.Lightweight.File;

namespace DotnetPackaging.Deb.Tests;

public class DebTests
{
    [Fact]
    public void Deb_test()
    {
        var deb = new DebFile(new ControlMetadata
        {
            Package = "Test",
            Version = "1.0-1",
            Section = "Utils",
            Priority = "optional",
            Architecture = "All",
            Maintainer = "Baeldung <test@test.com>",
            Description = "This is a test application\n for packaging",
        }, 
            new FileEntry(
                new RootedFile("something", new File("etc", String("NAME=Test"))), new UnixFileProperties()
                {
                    FileMode = UnixFilePermissions.Read | UnixFilePermissions.Write | UnixFilePermissions.GroupRead | UnixFilePermissions.OtherRead,
                    GroupId = 1000,
                    OwnerUsername = "jmn",
                    GroupName = "jmn",
                    LinkIndicator = 1,
                    OwnerId = 1000,
                    LastModification = 24.April(2024).AddHours(14).AddMinutes(11).AddSeconds(5)
                }));
    }
}
