namespace DotnetPackaging.Old.Deb;

public class ObservableStream : Stream
{
    private readonly IObservable<byte> observableBytes;
    private MemoryStream memoryStream = new MemoryStream();
    private IDisposable byteSubscription;

    public ObservableStream(IObservable<byte> observableBytes)
    {
        this.observableBytes = observableBytes;
        byteSubscription = this.observableBytes.Subscribe(OnNextByte, OnError, OnCompleted);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => memoryStream.Length;

    public override long Position
    {
        get => memoryStream.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        // No es necesario hacer nada en este caso.
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return memoryStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            byteSubscription.Dispose();
            memoryStream.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnNextByte(byte byteValue)
    {
        memoryStream.WriteByte(byteValue);
    }

    private void OnError(Exception ex)
    {
        // Maneja los errores si es necesario.
    }

    private void OnCompleted()
    {
        memoryStream.Seek(0, SeekOrigin.Begin); // Reinicia el puntero de lectura al principio.
    }
}