using System.IO.Abstractions;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Archives.Deb.Contents;
using DotnetPackaging.Archives.Deb;
using DotnetPackaging.Tests.Deb.EndToEnd;
using FluentAssertions;
using Serilog;
using Zafiro.FileSystem.Local;

namespace DotnetPackaging.Tests.Deb;

public class DebFileTests
{
    [Fact]
    public async Task FullDebTest()
    {
        var fs = new LocalFileSystem(new FileSystem(), Maybe<ILogger>.None);
        var result = await fs.GetDirectory("TestFiles/Content")
            .Bind(dir => ContentCollection.From(dir, TestData.ExecutableFiles))
            .Map(collection => new DebFile(TestData.Metadata, collection));
        IEnumerable<byte> expectedBytes = await File.ReadAllBytesAsync("TestFiles\\Sample.deb");
        result.Should().Succeed().And.Subject.Value.Bytes.ToEnumerable().Count().Should().Be(expectedBytes.Count());
    }

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