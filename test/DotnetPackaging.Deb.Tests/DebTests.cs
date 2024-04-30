using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions;
using FluentAssertions.Common;
using FluentAssertions.Extensions;
using Xunit;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using Directory = Zafiro.FileSystem.Lightweight.Directory;
using File = Zafiro.FileSystem.Lightweight.File;
using IoFile = System.IO.File;

namespace DotnetPackaging.Deb.Tests;

public class DebTests
{
    [Fact]
    public async Task Deb_test()
    {
        var metadata = new PackageMetadata
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

    private static IEnumerable<string> GetParentDirectories(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(directory))
        {
            yield return directory;
            directory = Path.GetDirectoryName(directory);
        }
    }
    
    [Fact]
    public async Task Create_deb_from_directory()
    {
        var directory = new Directory(
            "Somedirectory",
            new List<IFile>()
            {
                new File("MyApp", "Fake exe"),
                new File("MyApp.dll", "Fake dll"),
                new File("Some content.txt", "Hi"),
            }, new List<IDirectory>());

        var metadata = new PackageMetadata
        {
            AppName = "Test Application",
            AppId = "com.Company.TestApplication",
            Package = "test-application",
            Version = "1.0-1",
            Section = "utils",
            Priority = "optional",
            Architecture = "all",
            Maintainer = "Baeldung <test@test.com>",
            Description = "This is a test application\n for packaging",
            ModificationTime = 25.April(2024).AddHours(9).AddMinutes(47).AddSeconds(22).ToDateTimeOffset(),
            ExecutableName = "MyApp",
        };

        var appDir = $"/opt/{metadata.Package}";

        var filesInDirectory = await directory.GetFilesInTree(ZafiroPath.Empty);

        var result = await filesInDirectory.Bind(async files =>
        {
            var tarFileProperties = new TarFileProperties()
            {
                FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("644"),
                GroupId = 0,
                OwnerId = 0,
                GroupName = "root",
                OwnerUsername = "root",
                LastModification = DateTimeOffset.Now,
            };

            var executablePath = appDir + "/" + metadata.ExecutableName;

            var additionalFileEntries = new FileTarEntry[]
            {
                new($"./usr/share/{metadata.Package.ToLower()}.desktop", new StringByteProvider(MiscMixin.DesktopFileContents(executablePath, metadata), Encoding.ASCII), tarFileProperties),
                new($"./usr/bin/{metadata.Package.ToLower()}", new StringByteProvider(MiscMixin.RunScript(executablePath), Encoding.ASCII), tarFileProperties)
            };

            var directoryFileEntries = filesInDirectory.Value.Select(x => new FileTarEntry($"./opt/{metadata.Package}/" + x.FullPath(), x.Rooted, tarFileProperties));
            
            TarDirectoryProperties directoryProperties = new TarDirectoryProperties()
            {
                FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("644"),
                GroupId = 0,
                OwnerId = 0,
                GroupName = "root",
                OwnerUsername = "root",
                LastModification = DateTimeOffset.Now,
            };

            var allFiles = directoryFileEntries.Concat(additionalFileEntries).ToList();
            var directoryEntries = allFiles.Select(x => ((ZafiroPath)x.Path[2..]).Parents()).Flatten().Distinct().OrderBy(x => x.RouteFragments.Count());
            var dirEntries = directoryEntries.Select(path => (TarEntry)new DirectoryTarEntry($"./{path}", directoryProperties));

            var tarEntries = dirEntries.Concat(allFiles);

            var debFile = new DebFile(metadata, tarEntries.ToArray());
            await using var fileStream = IoFile.Open("C:\\Users\\JMN\\Desktop\\testing.deb", FileMode.Create);
            return (await debFile.ToByteProvider().DumpTo(fileStream).ToList()).Combine();
        });

        result.Should().Succeed();
    }
}