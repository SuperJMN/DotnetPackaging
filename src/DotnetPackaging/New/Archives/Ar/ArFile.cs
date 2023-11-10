using System.Reactive.Linq;
using DotnetPackaging.Common;

namespace DotnetPackaging.New.Archives.Ar;

public class ArFile : IByteFlow
{
    private readonly Entry[] entries;
    private const string SignatureString = "!<arch>\n";

    public ArFile(params Entry[] entries)
    {
        this.entries = entries;
    }

    public IObservable<byte> Bytes
    {
        get
        {
            var bytesFromEntries = entries
                .ToObservable()
                .Select(flow => flow.Bytes)
                .Concat();

            return Signature.Concat(bytesFromEntries);
        }
    }

    private IObservable<byte> Signature => SignatureString.GetAsciiBytes().ToObservable();

    public long Length => entries.Sum(e => e.Length) + SignatureString.Length;
}