using System.Reactive.Linq;

namespace DotnetPackaging.Deb.Bytes;

public static class Data
{
    public static IData FromString(string content) => FromString(content, System.Text.Encoding.UTF8);

    public static IData FromString(string content, System.Text.Encoding encoding)
    {
        var bytes = encoding.GetBytes(content);
        return FromByteArray(bytes);
    }

    public static IData FromByteArray(byte[] bytes)
    {
        return new SimpleData(bytes);
    }

    private class SimpleData : IData
    {
        private readonly byte[] bytes;

        public SimpleData(byte[] bytes)
        {
            this.bytes = bytes;
        }

        public IObservable<byte[]> Bytes => Observable.Return(bytes);

        public long Length => bytes.LongLength;

        public IDisposable Subscribe(IObserver<byte[]> observer)
        {
            return Bytes.Subscribe(observer);
        }
    }
}
