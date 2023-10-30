using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using Serilog;
using Zafiro.IO;

namespace Archiver.Tar;

public class Entry
{
    public int BlockSize { get; }
    private readonly EntryData entryData;
    private readonly Maybe<ILogger> logger;

    public Entry(EntryData entryData, Maybe<int> blockSize, Maybe<ILogger> logger)
    {
        BlockSize = blockSize.GetValueOrDefault(512);
        this.entryData = entryData;
        this.logger = logger;
    }

    public IObservable<byte> Bytes
    {
        get
        {
            var header = Header()
                .AsBlocks<byte>(BlockSize, 0);

            Log("Header", header);

            var content = Content().AsBlocks<byte>(BlockSize, 0);

            Log("Content", content);

            return header.Concat(content);
        }
    }

    private static IObservable<byte> ToAscii(string content) => Encoding.ASCII.GetBytes(content).ToObservable();

    private void Log(string name, IObservable<byte> header)
    {
        logger.Execute(l =>
        {
            header.ToList()
                .Take(1)
                .Do(list => l.Debug("{Item}: Size = {Bytes}", name, list.Count))
                .Subscribe();
        });
    }

    private IObservable<byte> Header()
    {
        return
            Header(ChecksumPlaceholder())
                .ToList()
                .Select(list => list.Sum(b => b))
                .SelectMany(checksum => Header(Checksum(checksum)));
    }

    private IObservable<byte> Checksum(int checksum)
    {
        var chars = Convert.ToString(checksum, 8).PadLeft(7).PadRight(8);
        var bytes = Encoding.ASCII.GetBytes(chars);
        return bytes.ToObservable();
    }

    private IObservable<byte> Header(IObservable<byte> checksum) => Observable.Concat
    (
        Filename(),
        FileMode(),
        Owner(),
        Group(),
        FileSize(),
        LastModification(),
        checksum,
        LinkIndicator(),
        NameOfLinkedFile()
    );

    private IObservable<byte> Content() => entryData.Contents.ToObservable();

    /// <summary>
    ///     From 156 to 157 Link indicator (file type)
    /// </summary>
    private IObservable<byte> LinkIndicator()
    {
        return new byte[] { 0x00 }.ToObservable();
    }

    /// <summary>
    ///     From 157 to 257 Link indicator (file type)
    /// </summary>
    private IObservable<byte> NameOfLinkedFile() => ToAscii("".ToFixed(100));


    private IObservable<byte> ChecksumPlaceholder()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20 }.ToObservable();
    }

    /// <summary>
    ///     From 124 to 136 (in octal)
    /// </summary>
    /// <param name="contents"></param>
    private IObservable<byte> FileSize() => entryData.Contents.Length.ToOctalField().GetAsciiBytes().ToObservable();

    /// <summary>
    ///     From 136 to 148 Last modification time in numeric Unix time format (octal)
    /// </summary>
    /// <param name="lastModification"></param>
    private IObservable<byte> LastModification() => entryData.Properties.LastModification.ToUnixTimeSeconds().ToOctalField().GetAsciiBytes().ToObservable();

    /// <summary>
    ///     From 116 to 124
    /// </summary>
    private IObservable<byte> Group()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x30, 0x00 }.ToObservable();
    }

    /// <summary>
    ///     From 100 to 108
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> FileMode()
    {
        return "664".NullTerminatedPaddedField(8).GetAsciiBytes().ToObservable();
    }

    /// <summary>
    ///     From 116 to 124
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> Owner()
    {
        return 1000L.ToOctal().NullTerminatedPaddedField(8).GetAsciiBytes().ToObservable();
    }

    /// <summary>
    ///     From 0 to 100
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> Filename() => ToAscii(entryData.Name.ToFixed(100));
}