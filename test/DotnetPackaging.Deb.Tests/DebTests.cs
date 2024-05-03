using System.Reactive.Linq;
using System.Text;
using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions;
using FluentAssertions.Common;
using FluentAssertions.Extensions;
using Xunit;
using Zafiro.FileSystem.Lightweight;
using Zafiro.Reactive;
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
            Architecture = Architecture.All,
            Maintainer = "Baeldung <test@test.com>",
            Description = "This is a test application\n for packaging",
            ModificationTime = 25.April(2024).AddHours(9).AddMinutes(47).AddSeconds(22).ToDateTimeOffset()
        };

        var shContents = """
                         #!/bin/bash
                         . test.conf
                         echo "Hi $NAME"

                         """.FromCrLfToLf();

        var confContents = "NAME=Test\n".FromCrLfToLf();

        var defaultFileProperties = new TarFileProperties
        {
            FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("777"),
            GroupId = 0,
            GroupName = "root",
            LastModification = DateTimeOffset.Now,
            OwnerId = 0,
            OwnerUsername = "root"
        };

        var defaultDirProperties = new TarDirectoryProperties
        {
            FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("755"),
            GroupId = 0,
            GroupName = "root",
            LastModification = 25.April(2024).AddHours(9).AddMinutes(47).AddSeconds(22).ToDateTimeOffset(),
            OwnerId = 0,
            OwnerUsername = "root"
        };

        var tarEntries = new TarEntry[]
        {
            new DirectoryTarEntry("./", defaultDirProperties with { LastModification = DateTimeOffset.Parse("24/04/2024 12:11:05 +00:00") }),
            new DirectoryTarEntry("./bin/", defaultDirProperties with { LastModification = DateTimeOffset.Parse("24/04/2024 12:10:16 +00:00") }),
            new FileTarEntry("./bin/test.sh", new StringData(shContents, Encoding.ASCII), defaultFileProperties with { LastModification = DateTimeOffset.Parse("24/04/2024 12:09:08 +00:00") }),
            new DirectoryTarEntry("./etc/", defaultDirProperties with { LastModification = DateTimeOffset.Parse("24/04/2024 12:10:10 +00:00") }),
            new FileTarEntry("./etc/test.conf", new StringData(confContents, Encoding.ASCII), defaultFileProperties with
            {
                FileMode = UnixFilePermissionsMixin.ParseUnixPermissions("644"),
                LastModification = DateTimeOffset.Parse("24/04/2024 12:06:22 +00:00")
            })
        };

        var deb = new DebFile(metadata, tarEntries);
        var actual = deb.ToByteProvider().Bytes.Flatten().ToEnumerable().ToArray();
        await IoFile.WriteAllBytesAsync(@"C:\Users\JMN\Desktop\actual.deb", actual);
        var expected = await IoFile.ReadAllBytesAsync("TestFiles/Sample.deb");

        actual.Should().BeEquivalentTo(expected);
    }

    //[Fact]
    //public async Task Create_deb_from_directory()
    //{
    //    var directory = new Directory(
    //        "Somedirectory",
    //        new List<IFile>
    //        {
    //            new File("MyApp", "Fake exe"),
    //            new File("MyApp.dll", "Fake dll"),
    //            new File("Some content.txt", "Hi")
    //        }, new List<IDirectory>());
        
    //    var metadata = new PackageMetadata
    //    {
    //        AppName = "Test Application",
    //        AppId = "com.Company.TestApplication",
    //        Package = "test-application",
    //        Version = "1.0-1",
    //        Section = "utils",
    //        Priority = "optional",
    //        Architecture = Architecture.All,
    //        Maintainer = "Baeldung <test@test.com>",
    //        Description = "This is a test application\n for packaging",
    //        ModificationTime = 25.April(2024).AddHours(9).AddMinutes(47).AddSeconds(22).ToDateTimeOffset(),
    //        ExecutableName = "MyApp",
    //        Icon = Maybe.From(await Icon.FromImage(new Image<Bgra32>(48, 48, Color.Red))),
    //    };
    //    var result = await DebPackageCreator.CreateFromDirectory(directory, metadata);
    //    result.Should().Succeed();
    //    await using var fileStream = IoFile.Open("C:\\Users\\JMN\\Desktop\\testing.deb", FileMode.Create);
    //    await result.Value.ToByteProvider().DumpTo(fileStream);
    //}
    
    //[Fact]
    //public async Task Integration()
    //{
    //    var fileSystem = new FileSystem();
    //    var directory = new SystemIODirectory(fileSystem.DirectoryInfo.New(@"C:\Users\JMN\Desktop\AppDir\AvaloniaSyncer"));
        
    //    var metadata = new PackageMetadata
    //    {
    //        AppName = "Avalonia Syncer",
    //        AppId = "com.SuperJMN.AvaloniaSyncer",
    //        Package = "avaloniasyncer",
    //        Version = "1.0.0",
    //        Section = "utils",
    //        Priority = "optional",
    //        Architecture = Architecture.X64,
    //        Maintainer = "SuperJMN <jmn@superjmn.com>",
    //        Description = "Application for cool file exploring",
    //        ModificationTime = DateTimeOffset.Now,
    //        ExecutableName = "AvaloniaSyncer.Desktop",
    //        Icon = Maybe.From(await Icon.FromImage(await Image.LoadAsync("E:\\Repos\\SuperJMN\\AvaloniaSyncer\\AppImage.png"))),
    //    };
    //    var result = await DebPackageCreator.CreateFromDirectory(directory, metadata);
    //    result.Should().Succeed();
    //    await using (var fileStream = IoFile.Open($"C:\\Users\\JMN\\Desktop\\{FileName.FromMetadata(metadata)}.deb", FileMode.Create))
    //    {
    //        (await result.Value.ToByteProvider().DumpTo(fileStream).ToList()).Combine();
    //    }
    //}
}