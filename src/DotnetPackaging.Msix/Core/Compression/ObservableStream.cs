namespace DotnetPackaging.Msix.Core.Compression;

public class ObservableStream : Stream
{
    private readonly IObserver<byte[]> _observer;

    public ObservableStream(IObserver<byte[]> observer)
    {
        _observer = observer;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        // Copiamos los bytes escritos y los enviamos directamente al observer.
        var chunk = new byte[count];
        Array.Copy(buffer, offset, chunk, 0, count);
        _observer.OnNext(chunk);
    }

    // Implementaciones necesarias, sin mucha cháchara:
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override void Flush() { }
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}