using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using Serilog;
using Zafiro.IO;

namespace Archiver.Tar;

public record Entry(string Name, Stream Contents);

public class Tar
{
    private const int BlockingFactor = 20 * BlockSize;
    private const int BlockSize = 512;
    private readonly Stream output;
    private readonly Maybe<ILogger> logger;

    public Tar(Stream output, Maybe<ILogger> logger)
    {
        this.output = output;
        this.logger = logger;
    }

    public IObservable<byte> Write(string name, Stream contents)
    {
        var header = WriteHeader(name)
            .AsBlocks<byte>(BlockSize, 0);

        Log("Header", header);

        var content = Content(contents).AsBlocks<byte>(BlockSize, 0);

        Log("Content", content);

        var endOfFile = EndOfFile;

        Log("EOF", endOfFile);

        return header.Concat(content).Concat(endOfFile);
    }

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

    private IObservable<byte> EndOfFile => Observable.Repeat<byte>(0x00, BlockSize * 2);

    public Result Build(params Entry[] entries)
    {
        var rawFile = entries
            .Select(entry => Write(entry.Name, entry.Contents))
            .Concat();

        var tarContents =
            rawFile
            .AsBlocks<byte>(BlockSize, 0x00);
                tarContents.DumpTo(output).Subscribe();

        return Result.Success();
    }

    private static IObservable<byte> ToAscii(string content) => Encoding.ASCII.GetBytes(content).ToObservable();

    private IObservable<byte> WriteHeader(string name)
    {
        return
            Header(name, ChecksumPlaceholder())
                .ToList()
                .Select(list => list.Sum(b => b))
                .SelectMany(checksum => Header(name, Checksum(checksum)));
    }

    private IObservable<byte> Checksum(int checksum)
    {
        var chars = Convert.ToString(checksum, 8).PadLeft(7).PadRight(8);
        var bytes = Encoding.ASCII.GetBytes(chars);
        return bytes.ToObservable();
    }

    private IObservable<byte> Header(string name, IObservable<byte> checksum) => Observable.Concat
    (
        Filename(name),
        FileMode(),
        Owner(),
        Group(),
        FileSize(),
        LastModification(),
        checksum,
        LinkIndicator(),
        NameOfLinkedFile()
    );

    private IObservable<byte> Content(Stream stream) => stream.ToObservable();

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
    private IObservable<byte> FileSize()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x36, 0x37, 0x37, 0x00 }.ToObservable();
    }

    /// <summary>
    ///     From 136 to 148 Last modification time in numeric Unix time format (octal)
    /// </summary>
    private IObservable<byte> LastModification()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x30, 0x00 }.ToObservable();
    }

    /// <summary>
    ///     From 116 to 124
    /// </summary>
    private IObservable<byte> Group()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x30, 0x00 }.ToObservable();
    }

    /// <summary>
    /// From 100 to 108
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> FileMode()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x37, 0x37, 0x37, 0x0 }.ToObservable();
    }

    /// <summary>
    /// From 116 to 124
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> Owner()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x30, 0x00 }.ToObservable();
    }

    /// <summary>
    /// From 0 to 100
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    private IObservable<byte> Filename(string filename) => ToAscii(filename.ToFixed(100));
}