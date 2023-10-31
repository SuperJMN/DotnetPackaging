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

    public Entry(EntryData entryData, Maybe<ILogger> logger, int blockSize = 512)
    {
        this.entryData = entryData;
        this.logger = logger;
        BlockSize = blockSize;
    }

    public IObservable<byte> Bytes
    {
        get
        {
            var header = Header()
                .AsBlocks<byte>(BlockSize, 0);
            var content = Contents().AsBlocks<byte>(BlockSize, 0);
            var bytes = header.Concat(content);
            return bytes;
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

    /// <summary>
    /// From 148 to 156 [8]
    /// </summary>
    /// <param name="checksum"></param>
    /// <returns></returns>
    private IObservable<byte> Checksum(int checksum)
    {
        return (checksum.ToOctal().PadLeft(6, '0').NullTerminated() + " ").GetAsciiBytes().ToObservable();
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
        NameOfLinkedFile(),
        Ustar(),
        UstarVersion(),
        OwnerUsername(),
        GroupUsername()
    );

    private IObservable<byte> GroupUsername()
    {
        return entryData.Properties.GroupName
            .Map(s => s.PadRight(32, '\0').GetAsciiBytes().ToObservable())
            .GetValueOrDefault(() => Observable.Repeat<byte>(0x00, 32));
    }

    private IObservable<byte> OwnerUsername()
    {
        return entryData.Properties.OwnerUsername
            .Map(s => s.PadRight(32, '\0').GetAsciiBytes().ToObservable())
            .GetValueOrDefault(() => Observable.Repeat<byte>(0x00, 32));
    }

    private IObservable<byte> UstarVersion()
    {
        return new byte []{ 0x20, 0x00 }.ToObservable();
    }

    private IObservable<byte> Ustar()
    {
        return "ustar".PadRight(6, ' ').GetAsciiBytes().ToObservable();
    }

    private IObservable<byte> Contents()
    {
        return Observable.Using(() => entryData.Contents(), stream => stream.ToObservable());
    }

    /// <summary>
    ///     From 156 to 157 Link indicator (file type)
    /// </summary>
    private IObservable<byte> LinkIndicator()
    {
        return "0".GetAsciiBytes().ToObservable();
    }

    /// <summary>
    ///     From 157 to 257 Link indicator (file type)
    /// </summary>
    private IObservable<byte> NameOfLinkedFile() => Observable.Repeat<byte>(0x00, 100);

    private IObservable<byte> ChecksumPlaceholder()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20 }.ToObservable();
    }

    /// <summary>
    ///     From 124 to 136 (in octal)
    /// </summary>
    private IObservable<byte> FileSize() => Observable.Using(() => entryData.Contents(), stream => stream.Length.ToOctalField().GetAsciiBytes().ToObservable());

    /// <summary>
    ///     From 136 to 148 Last modification time in numeric Unix time format (octal)
    /// </summary>
    private IObservable<byte> LastModification() => entryData.Properties.LastModification.ToUnixTimeSeconds().ToOctalField().GetAsciiBytes().ToObservable();

    /// <summary>
    ///     From 100 to 108
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> FileMode()
    {
        return entryData.Properties.FileModes.ToString().NullTerminatedPaddedField(8).GetAsciiBytes().ToObservable();
    }

    /// <summary>
    ///     From 8 to 116
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> Owner()
    {
        return entryData.Properties.OwnerId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8).GetAsciiBytes().ToObservable();
    }

    /// <summary>
    ///     From 116 to 124
    /// </summary>
    private IObservable<byte> Group()
    {
        return entryData.Properties.GroupId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8).GetAsciiBytes().ToObservable();
    }

    /// <summary>
    ///     From 0 to 100
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> Filename() => ToAscii(entryData.Name.ToFixed(100));
}