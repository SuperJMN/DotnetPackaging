using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging.Deb.Archives.Tar;
using FluentAssertions.Common;
using FluentAssertions.Extensions;
using Zafiro.DataModel;
using Zafiro.FileSystem.Unix;
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
            Name = "Testa",
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
            FileMode = "777".ToFileMode(),
            GroupId = 0,
            GroupName = "root",
            LastModification = DateTimeOffset.Now,
            OwnerId = 0,
            OwnerUsername = "root"
        };

        var defaultDirProperties = new TarDirectoryProperties
        {
            FileMode = "755".ToFileMode(),
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
                FileMode = "644".ToFileMode(),
                LastModification = DateTimeOffset.Parse("24/04/2024 12:06:22 +00:00")
            })
        };

        var deb = new Archives.Deb.DebFile(metadata, tarEntries);
        var actual = deb.ToData().Bytes.Flatten().ToEnumerable().ToArray();
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
    //            new SlimFile("MyApp", "Fake exe"),
    //            new SlimFile("MyApp.dll", "Fake dll"),
    //            new SlimFile("Some content.txt", "Hi")
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
    //    await result.Value.ToData().DumpTo(fileStream);
    //}
   
}