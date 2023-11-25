using System.Reactive.Linq;
using DotnetPackaging.Common;

namespace DotnetPackaging.Archives.Tar;

public class TarFile : IByteFlow
{
    private const int BlockingFactor = 20 * BlockSize;
    private const int BlockSize = 512;
    private readonly Entry[] entries;

    public TarFile(params Entry[] entries)
    {
        this.entries = entries;
    }
    
    private long TotalSize => EntriesSize.RoundUpToNearestMultiple(BlockingFactor);

    private long EntriesSize
    {
        get { return entries.Sum(e => e.Length); }
    }

    public IObservable<byte> Bytes
    {
        get
        {
            return
                entries
                    .ToObservable()
                    .Select(flow => flow.Bytes)
                    .Concat()
                    .AsBlocks<byte>(BlockingFactor, 0x00);
        }
    }

    public IObservable<byte> BytesNew
    {
        get
        {
            return
                entries
                    .ToObservable()
                    .Select(flow => flow.Bytes)
                    .Concat()
                    .ConcatPadding((int) TotalSize - (int) EntriesSize, 0x00);
        }
    }

    public long Length => TotalSize;
}