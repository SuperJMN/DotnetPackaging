using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using MoreLinq;
using Zafiro.IO;

namespace Archiver.Tar;

public record Entry(string Name, Stream Contents);

public class Tar
{
    private readonly int blockSize = 512;
    private readonly Stream output;

    public Tar(Stream output)
    {
        this.output = output;
    }

    public void Write(string name, Stream contents)
    {
        var header = WriteHeader(name)
            .BlocksWithPadding<byte>(blockSize, 0);

        var content = Content(contents).BlocksWithPadding<byte>(blockSize, 0);

        header.Concat(content).Concat(EndOfFile)
            .DumpTo(output)
            .Subscribe();
    }

    private IObservable<byte> EndOfFile => new byte[blockSize].ToObservable().Repeat(2);

    public Result Build(params Entry[] entries)
    {
        entries.ForEach(entry => Write(entry.Name, entry.Contents));
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