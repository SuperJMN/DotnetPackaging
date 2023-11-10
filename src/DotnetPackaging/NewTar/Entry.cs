using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Common;
using System.Text;
using DotnetPackaging.Tar;
using Zafiro.FileSystem;
using Zafiro.IO;

namespace DotnetPackaging.NewTar;

public class Entry : IByteFlow
{
    public int BlockSize { get; }

    private readonly string name;
    private readonly Properties properties;
    private readonly ByteFlow byteFlow;

    public Entry(string name, Properties properties, ByteFlow byteFlow, int blockSize = 512)
    {
        this.name = name;
        this.properties = properties;
        this.byteFlow = byteFlow;
    }

    public long Length => byteFlow.Length.RoundUpToNearestMultiple(BlockSize) + BlockSize;

    public IObservable<byte> Bytes
    {
        get
        {
            var header = Header()
                .AsBlocks<byte>(BlockSize, 0);
            var content = byteFlow.Origin
                .AsBlocks<byte>(BlockSize, 0);
            var bytes = header.Concat(content);
            return bytes;
        }
    }

    private static IObservable<byte> ToAscii(string content) => Encoding.ASCII.GetBytes(content).ToObservable();

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
        return properties.GroupName
            .Map(s => s.PadRight(32, '\0').GetAsciiBytes().ToObservable())
            .GetValueOrDefault(() => Observable.Repeat<byte>(0x00, 32));
    }

    private IObservable<byte> OwnerUsername()
    {
        return properties.OwnerUsername
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

    /// <summary>
    ///     From 156 to 157 Link indicator (file type)
    /// </summary>
    private IObservable<byte> LinkIndicator()
    {
        return properties.LinkIndicator.ToString().GetAsciiBytes().ToObservable();
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
    private IObservable<byte> FileSize() => properties.Length.ToOctalField().GetAsciiBytes().ToObservable();

    /// <summary>
    ///     From 136 to 148 Last modification time in numeric Unix time format (octal)
    /// </summary>
    private IObservable<byte> LastModification() => properties.LastModification.ToUnixTimeSeconds().ToOctalField().GetAsciiBytes().ToObservable();

    /// <summary>
    ///     From 100 to 108
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> FileMode()
    {
        return properties.FileMode.ToString().NullTerminatedPaddedField(8).GetAsciiBytes().ToObservable();
    }

    /// <summary>
    ///     From 108 to 116
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> Owner()
    {
        return properties.OwnerId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8).GetAsciiBytes().ToObservable();
    }

    /// <summary>
    ///     From 116 to 124
    /// </summary>
    private IObservable<byte> Group()
    {
        return properties.GroupId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8).GetAsciiBytes().ToObservable();
    }

    /// <summary>
    ///     From 0 to 100
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> Filename() => ToAscii(name.Truncate(100).PadRight(100, '\0'));

    public static Ar.EntryData FromStream(string name, Func<FileStream> openRead)
    {
        var length = openRead().Length;

        var properties = new Ar.Properties()
        {
            Length = length,
            FileMode = Common.FileMode.Parse("644"),
            GroupId = 1000,
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
        };

        return new Ar.EntryData(name, properties, () => Observable.Using(openRead, stream => stream.ToObservable()));
    }

    public static Ar.EntryData FromString(string name, string str)
    {
        var length = str.Length;

        var properties = new Ar.Properties()
        {
            Length = length,
            FileMode = Common.FileMode.Parse("644"),
            GroupId = 1000,
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
        };

        return new Ar.EntryData(name, properties, () => str.GetAsciiBytes().ToObservable());
    }

    public static Task<Result<Ar.EntryData>> FromFile(IZafiroFile file)
    {
        return FromFile(file.Path.Name(), file);
    }

    public static Task<Result<Ar.EntryData>> FromFile(string name, IZafiroFile file)
    {
        var length = file.Size();

        return from size in file.Size()
            from data in file.GetContents()
            
            select new Ar.EntryData(name, new Ar.Properties()
            {
                Length = size,
                FileMode = Common.FileMode.Parse("644"),
                GroupId = 1000,
                LastModification = DateTimeOffset.Now,
                OwnerId = 1000,
            }, () => data.ToObservable());
    }
}

public interface IByteFlow
{
}