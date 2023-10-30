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

    public Result Build(params EntryData[] entries)
    {
        var tarContents =
            entries
                .Select(entry => new Entry(entry, BlockSize, logger).Bytes)
                .Concat()
                .AsBlocks<byte>(BlockingFactor, 0x00);

        tarContents.DumpTo(output).Subscribe();

        return Result.Success();
    }
}