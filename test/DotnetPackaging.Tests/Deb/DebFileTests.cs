using System.Reactive.Linq;
using DotnetPackaging.Common;
using DotnetPackaging.Old.Deb;
using Zafiro.FileSystem;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Deb;

public class DebFileTests
{
    // TODO: Place some interesting tests here
    //[Fact]
    //public async Task FullDebTest()
    //{
    //    var debFile = DebFile();

    //    await using var fileStream = File.Create("C:\\Users\\JMN\\Desktop\\Testing\\FullDebTest.deb");
    //    await debFile.Bytes.DumpTo(fileStream);
    //}

    //[Fact]
    //public async Task WriteControlTar()
    //{
    //    var debFile = DebFile();

    //    await using var output = File.Create("C:\\Users\\JMN\\Desktop\\Testing\\control.tar");
    //    await debFile.ControlTar().Bytes.DumpTo(output);
    //}

    //private static DebFile DebFile()
    //{
    //    var debFile = new DebFile(TestData.Metadata(), TestData.Contents());

    //    return debFile;
    //}
}