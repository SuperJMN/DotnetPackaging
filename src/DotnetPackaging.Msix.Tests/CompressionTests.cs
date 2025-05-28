using System.Reactive.Linq;
using DotnetPackaging.Msix.Core.Compression;
using Xunit;
using Zafiro.Reactive;

namespace MsixPackaging.Tests;

public class CompressionTests
{
    [Fact]
    public async Task Store_compression_gives_different_bytes() 
    {
        var bytes = "hola que tal va la jodida cosa"u8.ToArray();

        var source = bytes.ToObservable().Buffer(10).Select(list => list.ToArray());
        var actualBytes = await source.Compressed().ToList().Flatten();

        Assert.False(bytes.SequenceEqual(actualBytes));
    }
}