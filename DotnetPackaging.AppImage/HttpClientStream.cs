namespace DotnetPackaging.AppImage;

public class HttpClientStream : Stream
{
    private readonly Stream responseStream; // El Stream real con los datos de la respuesta.
    private readonly HttpClient httpClient; // HttpClient que necesita ser desechado.

    public HttpClientStream(HttpClient httpClient, Stream responseStream)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
    }

    public override bool CanRead => responseStream.CanRead;
    public override bool CanSeek => responseStream.CanSeek;
    public override bool CanWrite => responseStream.CanWrite;
    public override long Length => responseStream.Length;

    public override long Position
    {
        get => responseStream.Position;
        set => responseStream.Position = value;
    }

    public override void Flush() => responseStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) => responseStream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => responseStream.Seek(offset, origin);

    public override void SetLength(long value) => responseStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => responseStream.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            responseStream?.Dispose();
            httpClient?.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return await responseStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await responseStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await responseStream.FlushAsync(cancellationToken);
    }
}