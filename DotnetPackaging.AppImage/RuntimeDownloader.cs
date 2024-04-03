using System.Runtime.InteropServices;

namespace DotnetPackaging.AppImage;

public static class RuntimeDownloader
{
    private static readonly Dictionary<Architecture, string> RuntimeUrls = new()
    {
        { Architecture.X86, "https://github.com/AppImage/type2-runtime/releases/download/old/runtime-fuse3-x86_64" },
        { Architecture.X64, "https://github.com/AppImage/type2-runtime/releases/download/old/runtime-fuse3-x86_64" },
        { Architecture.Arm, "https://github.com/AppImage/type2-runtime/releases/download/old/runtime-fuse2-armhf" },
        { Architecture.Arm64, "https://github.com/AppImage/type2-runtime/releases/download/old/runtime-fuse2-aarch64" },
    };

    public static Task<Stream> GetRuntimeStream(Architecture architecture, IHttpClientFactory httpClientFactory)
    {
        if (!RuntimeUrls.TryGetValue(architecture, out var runtimeUrl))
        {
            throw new ArgumentException("Invalid architecture", nameof(architecture));
        }
        return Download(runtimeUrl, httpClientFactory);
    }
    private static async Task<Stream> Download(string runtimeUrl, IHttpClientFactory httpClientFactory)
    {
        using var client = httpClientFactory.CreateClient();
        var streamAsync = await client.GetStreamAsync(runtimeUrl);
        streamAsync.ReadByte();
        return Http.Instance.GetStream(runtimeUrl);
    }
}

public class Http
{
    private readonly IHttpClientFactory httpClientFactory;

    private Http(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    public HttpClientStream GetStream(string uri)
    {
        return GetStreamAsync(new Uri(uri)).GetAwaiter().GetResult();
    }

    private async Task<HttpClientStream> GetStreamAsync(Uri uri)
    {
        var httpClient = new HttpClient();
        var responseStream = await httpClientFactory.CreateClient().GetStreamAsync(uri);
        return new HttpClientStream(httpClient, responseStream);
    }

    public static Http Instance { get; } = new Http(new DefaultHttpClientFactory());
}

public sealed class DefaultHttpClientFactory : IHttpClientFactory, IDisposable
{
    private readonly Lazy<HttpMessageHandler> _handlerLazy = new (() => new HttpClientHandler());

    public HttpClient CreateClient(string name) => new (_handlerLazy.Value, disposeHandler: false);

    public void Dispose()
    {
        if (_handlerLazy.IsValueCreated)
        {
            _handlerLazy.Value.Dispose();
        }
    }
}

public class HttpClientStream : Stream
{
    private Stream responseStream; // El Stream real con los datos de la respuesta.
    private HttpClient httpClient; // HttpClient que necesita ser desechado.

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
