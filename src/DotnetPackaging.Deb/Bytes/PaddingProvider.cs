using System.Reactive.Linq;

namespace DotnetPackaging.Deb.Bytes;

    public class PaddingProvider : IData
    {
        private readonly byte value;
        private readonly int count;

    public PaddingProvider(byte value, int count)
    {
        this.value = value;
        this.count = count;
    }
        public IObservable<byte[]> Bytes => Observable.Return(Enumerable.Repeat(value, count).ToArray());
        public long Length => count;

        public IDisposable Subscribe(IObserver<byte[]> observer)
        {
            return Bytes.Subscribe(observer);
        }
    }