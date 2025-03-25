using System.Reactive.Linq;
using BlockCompressor;
using Zafiro.Mixins;
using Zafiro.Reactive;

namespace BlockCompressorTests;

public class MsixCompressionTest
{
    [Fact]
    public async Task Test()
    {
        var path = @"HelloWorld.dat";
        await using var file = File.OpenRead(path);

        var blocks = await Compressed.Blocks(file.ToObservable()).ToList();
        var compressedBytes = blocks.Select(x => x.CompressedData).Flatten().ToArray();

        var originalBytes = DeflateHelper.DecompressDeflateData(compressedBytes);
        Assert.True((await File.ReadAllBytesAsync(path)).SequenceEqual(originalBytes));
    }
}