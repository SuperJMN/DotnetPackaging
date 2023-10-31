using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using Serilog;
using Zafiro.IO;

namespace Archiver.Tar;

public class Tar
{
    private const int BlockingFactor = 20 * BlockSize;
    private const int BlockSize = 512;
    private readonly Maybe<ILogger> logger;
    private readonly Stream output;

    public Tar(Stream output, Maybe<ILogger> logger)
    {
        this.output = output;
        this.logger = logger;
    }

    private IObservable<byte> EndOfFile => Observable.Repeat<byte>(0x00, BlockSize * 2);

    public async Task Build(params EntryData[] entries)
    {
        var tarContents =
            entries
                .Select(entry => new Entry(entry, logger).Bytes)
                .Concat()
                .AsBlocks<byte>(BlockingFactor, 0x00);

        await tarContents.DumpTo(output);
    }
}