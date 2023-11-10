using System.Reactive.Linq;
using DotnetPackaging.Common;

namespace DotnetPackaging.Old.Ar;

public class ArFile
{
    private readonly EntryData[] entries;

    public ArFile(params EntryData[] entries)
    {
        this.entries = entries;
    }

    public IObservable<byte> Bytes
    {
        get
        {
            var arContents = entries
                .Select(entry => new Entry(entry).Bytes)
                .Concat();

            return Signature.Concat(arContents);
        }
    }
    
    private IObservable<byte> Signature => "!<arch>\n".GetAsciiBytes().ToObservable();
}