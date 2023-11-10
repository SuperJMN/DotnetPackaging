using System.Reactive.Linq;
using DotnetPackaging.Common;

namespace DotnetPackaging.New.Ar;

public class Entry : IByteFlow
{
    private readonly string name;
    private readonly Properties properties;
    private readonly ByteFlow byteFlow;
    private const int HeaderSize = 68;

    public Entry(string name, Properties properties, ByteFlow byteFlow)
    {
        this.name = name;
        this.properties = properties;
        this.byteFlow = byteFlow;
    }

    public long Length => HeaderSize + byteFlow.Length;

    public IObservable<byte> Bytes
    {
        get
        {
            var header = Header();
            var bytes = header.Concat(Contents());
            return bytes;
        }
    }

    private IObservable<byte> Header() => Observable.Concat(
        FileIdentifier(),
        FileModificationTimestamp(),
        OwnerId(),
        GroupId(),
        FileMode(),
        FileSize(),
        Ending());

    /// <summary>
    ///     0	16	File identifier	ASCII
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> FileIdentifier() => name.PadRight(16).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     16	12	File modification timestamp (in seconds)	Decimal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> FileModificationTimestamp() => properties.LastModification.ToUnixTimeSeconds().ToString().PadRight(12).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     28	6	Owner ID	Decimal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> OwnerId() => properties.OwnerId.GetValueOrDefault().ToString().PadRight(6).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     34	6	Group ID	Decimal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> GroupId() => properties.GroupId.GetValueOrDefault().ToString().PadRight(6).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     40	8	File mode (type and permission)	Octal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> FileMode() => ("100" + properties.FileMode).PadRight(8).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     48	10	File size in bytes	Decimal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> FileSize()
    {
        return byteFlow.Length.ToString().PadRight(10).GetAsciiBytes().ToObservable();
    }

    /// <summary>
    ///     58	2	Ending characters	0x60 0x0A
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> Ending() => "`\n".GetAsciiBytes().ToObservable();

    private IObservable<byte> Contents()
    {
        return byteFlow.Bytes;
    }
}