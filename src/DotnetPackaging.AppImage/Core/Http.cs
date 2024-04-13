using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public class Http
{
    private readonly IHttpClientFactory httpClientFactory;

    private Http(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    public Task<Result<HttpClientStream>> GetStream(string uri)
    {
        return Result.Try(() => GetStreamAsync(new Uri(uri)));
    }

    private async Task<HttpClientStream> GetStreamAsync(Uri uri)
    {
        var httpClient = new HttpClient();
        var responseStream = await httpClientFactory.CreateClient().GetStreamAsync(uri);
        return new HttpClientStream(httpClient, responseStream);
    }

    public static Http Instance { get; } = new Http(new DefaultHttpClientFactory());
}