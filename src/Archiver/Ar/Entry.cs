using System.Reactive.Linq;
using DotnetPackaging.Common;

namespace DotnetPackaging.Ar;

public class Entry
{
    private readonly EntryData entryData;
    
    public Entry(EntryData entryData)
    {
        this.entryData = entryData;
    }

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
    private IObservable<byte> FileIdentifier() => entryData.Name.PadRight(16).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     16	12	File modification timestamp (in seconds)	Decimal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> FileModificationTimestamp() => entryData.Properties.LastModification.ToUnixTimeSeconds().ToString().PadRight(12).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     28	6	Owner ID	Decimal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> OwnerId() => entryData.Properties.OwnerId.GetValueOrDefault().ToString().PadRight(6).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     34	6	Group ID	Decimal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> GroupId() => entryData.Properties.GroupId.GetValueOrDefault().ToString().PadRight(6).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     40	8	File mode (type and permission)	Octal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> FileMode() => ("100" + entryData.Properties.FileMode).PadRight(8).GetAsciiBytes().ToObservable();

    /// <summary>
    ///     48	10	File size in bytes	Decimal
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> FileSize()
    {
        return entryData.Properties.Length.ToString().PadRight(10).GetAsciiBytes().ToObservable();
    }

    /// <summary>
    ///     58	2	Ending characters	0x60 0x0A
    /// </summary>
    /// <returns></returns>
    private IObservable<byte> Ending() => "`\n".GetAsciiBytes().ToObservable();

    private IObservable<byte> Contents()
    {
        return entryData.Contents();
    }
}