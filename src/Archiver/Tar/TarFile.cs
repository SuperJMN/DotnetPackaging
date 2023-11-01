using System.Reactive.Linq;

namespace Archiver.Tar;

public class TarFile
{
    private readonly EntryData[] entries;
    private const int BlockingFactor = 20 * BlockSize;
    private const int BlockSize = 512;

    public TarFile(params EntryData[] entries)
    {
        this.entries = entries;
    }

    public IObservable<byte> Bytes
    {
        get
        {
            return 
                entries
                    .Select(entry => new Entry(entry).Bytes)
                    .Concat()
                    .AsBlocks<byte>(BlockingFactor, 0x00);
        }
    }

    private IObservable<byte> EndOfFile => Observable.Repeat<byte>(0x00, BlockSize * 2);

    public long Length => Bytes.ToEnumerable().Count();
}