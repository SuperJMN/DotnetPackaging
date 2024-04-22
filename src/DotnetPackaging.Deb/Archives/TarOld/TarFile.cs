using System.Reactive.Linq;

namespace DotnetPackaging.Deb.Archives.TarOld;

public class TarFile : IByteFlow
{
    private readonly Entry[] entries;
    private const int BlockingFactor = 20 * BlockSize;
    private const int BlockSize = 512;

    public TarFile(params Entry[] entries)
    {
        this.entries = entries;
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

    public long Length => entries.Sum(e => e.Length).RoundUpToNearestMultiple(BlockingFactor);
}