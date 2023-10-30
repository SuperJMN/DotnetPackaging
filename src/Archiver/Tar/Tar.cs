using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using Serilog;
using SharpCompress.Common;
using Zafiro.IO;

namespace Archiver.Tar;

public record Entry(string Name, Properties Properties, Stream Contents);

public class Properties
{
    public Properties(DateTimeOffset lastModification)
    {
        LastModification = lastModification;
    }

    public DateTimeOffset LastModification { get; private set; }
}

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

    public IObservable<byte> Write(Entry entry)
    {
        var header = WriteHeader(entry)
            .AsBlocks<byte>(BlockSize, 0);

        Log("Header", header);

        var content = Content(entry).AsBlocks<byte>(BlockSize, 0);

        Log("Content", content);

        var endOfFile = EndOfFile;

        Log("EOF", endOfFile);

        return header.Concat(content).Concat(endOfFile);
    }

    public Result Build(params Entry[] entries)
    {
        var rawFile = entries
            .Select(entry => Write(entry))
            .Concat();

        var tarContents =
            rawFile
                .AsBlocks<byte>(BlockingFactor, 0x00);
        tarContents.DumpTo(output).Subscribe();

        return Result.Success();
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

    private IObservable<byte> WriteHeader(Entry entry)
    {
        return
            Header(entry, ChecksumPlaceholder())
                .ToList()
                .Select(list => list.Sum(b => b))
                .SelectMany(checksum => Header(entry, Checksum(checksum)));
    }

    private IObservable<byte> Checksum(int checksum)
    {
        var chars = Convert.ToString(checksum, 8).PadLeft(7).PadRight(8);
        var bytes = Encoding.ASCII.GetBytes(chars);
        return bytes.ToObservable();
    }

    private IObservable<byte> Header(Entry entry, IObservable<byte> checksum) => Observable.Concat
    (
        Filename(entry),
        FileMode(),
        Owner(),
        Group(),
        FileSize(entry),
        LastModification(entry),
        checksum,
        LinkIndicator(),
        NameOfLinkedFile()
    );

    private IObservable<byte> Content(Entry entry) => entry.Contents.ToObservable();

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
    private IObservable<byte> FileSize(Entry entry) => entry.Contents.Length.ToOctalField().GetAsciiBytes().ToObservable();

    /// <summary>
    ///     From 136 to 148 Last modification time in numeric Unix time format (octal)
    /// </summary>
    /// <param name="lastModification"></param>
    private IObservable<byte> LastModification(Entry entry) => entry.Properties.LastModification.ToUnixTimeSeconds().ToOctalField().GetAsciiBytes().ToObservable();

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
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x37, 0x37, 0x37, 0x0 }.ToObservable();
    }

    /// <summary>
    ///     From 116 to 124
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> Owner()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x30, 0x00 }.ToObservable();
    }

    /// <summary>
    ///     From 0 to 100
    /// </summary>
    /// <param name="entry"></param>
    /// <returns></returns>
    private IObservable<byte> Filename(Entry entry) => ToAscii(entry.Name.ToFixed(100));
}