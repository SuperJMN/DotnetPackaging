using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Ar;
using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Archives.Tar;
using DotnetPackaging.Deb.Tests;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using Zafiro.FileSystem.Lightweight;
using static DotnetPackaging.Deb.Tests.Mixin;
using File = Zafiro.FileSystem.Lightweight.File;
using IoFile = System.IO.File;

namespace DotnetPackaging.Deb.Tests;

public class DebTests
{
    [Fact]
    public async Task Deb_test()
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
                new RootedFile("etc", new File("etc", String("NAME=Test"))), new UnixFileProperties()
                {
                    FileMode = UnixFilePermissions.Read | UnixFilePermissions.Write | UnixFilePermissions.GroupRead | UnixFilePermissions.OtherRead,
                    GroupId = 1000,
                    OwnerUsername = "jmn",
                    GroupName = "jmn",
                    LinkIndicator = 1,
                    OwnerId = 1000,
                    LastModification = 24.April(2024).AddHours(14).AddMinutes(11).AddSeconds(5)
                }));

        var memoryStream = new MemoryStream();

        var result = await DebFileWriter.Write(deb, memoryStream);
        result.Should().Succeed();
        await using (var fileStream = IoFile.Open("C:\\Users\\JMN\\Desktop\\Sample.deb", FileMode.Create))
        {
            memoryStream.WriteTo(fileStream);
        }

        memoryStream.Should().BeEquivalentTo(IoFile.OpenRead("TestFiles/Sample.deb"));
    }
}


public class DebFileWriter
{
    public static async Task<Result> Write(DebFile deb, MemoryStream stream)
    {
        var data = $"""
                    Package: {deb.ControlMetadata.Package}
                    Priority: {deb.ControlMetadata.Priority}
                    Section: {deb.ControlMetadata.Section}
                    Maintainer: {deb.ControlMetadata.Maintainer}
                    Version: {deb.ControlMetadata.Version}
                    Homepage: {deb.ControlMetadata.Homepage}
                    Vcs-Git: {deb.ControlMetadata.VcsGit}
                    Vcs-Browser: {deb.ControlMetadata.VcsBrowser}
                    Architecture: {deb.ControlMetadata.Architecture}
                    License: {deb.ControlMetadata.License}
                    Installed-Size: {deb.ControlMetadata.InstalledSize}
                    Recommends: {deb.ControlMetadata.Recommends}
                    Description: {deb.ControlMetadata.Description}
                    """.FromCrLfToLf();

        var contents = """
                       2.0

                       """.FromCrLfToLf();

        var properties = new Properties()
        {
            FileMode = (UnixFilePermissions) Convert.ToInt32("644", 8),
            GroupId = 1000,
            LastModification = 12.April(2020),
            OwnerId = 1000,
        };

        var controlFile =
            new ArFile
            (
                new Entry(new File("debian-binary", String(contents)), properties),
                new Entry(new File("control", String(data)), properties)
            );

        await ArWriter.Write(controlFile, stream);

        return Result.Success();
    }
}
