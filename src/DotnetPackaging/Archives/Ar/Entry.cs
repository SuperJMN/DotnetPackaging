using System.Reactive.Linq;
using DotnetPackaging.Common;

namespace DotnetPackaging.Archives.Ar;

public class Entry : IByteFlow
{
    public string Name { get; }
    public Properties Properties { get; }
    private readonly IByteFlow byteFlow;
    private const int HeaderSize = 68;

    public Entry(string name, Properties properties, IByteFlow byteFlow)
    {
        Name = name;
        Properties = properties;
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
    private IObservable<byte> FileIdentifier() => Name.PadRight(16).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     16	12	File modification timestamp (in seconds)	Decimal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> FileModificationTimestamp() => Properties.LastModification.ToUnixTimeSeconds().ToString().PadRight(12).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     28	6	Owner ID	Decimal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> OwnerId() => Properties.OwnerId.GetValueOrDefault().ToString().PadRight(6).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     34	6	Group ID	Decimal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> GroupId() => Properties.GroupId.GetValueOrDefault().ToString().PadRight(6).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     40	8	File mode (type and permission)	Octal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> FileMode() => ("100" + Properties.FileMode).PadRight(8).GetAsciiBytes().ToObservable();

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