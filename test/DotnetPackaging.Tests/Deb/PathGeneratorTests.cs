using DotnetPackaging.Old.Deb;
using FluentAssertions;
using Zafiro.FileSystem;

namespace DotnetPackaging.Tests.Deb;

public class PathGeneratorTests
{
    // TODO: Place some interesting tests here
    //[Fact]
    //public void Directories()
    //{
    //    var sut = new DebPaths("SamplePackage", new[]
    //    {
    //        new ZafiroPath("Subdir/File1.txt"),
    //        new ZafiroPath("Subdir/File2.txt")
    //    });

    //    var dirs = sut.Directories().Select(x => x.Path);
    //    dirs.Should().BeEquivalentTo(new []
    //    {
    //        ".",
    //        "./usr",
    //        "./usr/local",
    //        "./usr/local/bin",
    //        "./usr/local/bin/SamplePackage",
    //        "./usr/local/bin/SamplePackage/Subdir",
    //    });
    //}
}