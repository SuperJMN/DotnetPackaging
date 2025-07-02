using Zafiro.DataModel;

namespace DotnetPackaging.AppImage.Core;

public static class HttpRequestData
{
    private static readonly IHttpClientFactory HttpClientFactory = new DefaultHttpClientFactory();

    public static Task<Result<IData>> Create(Uri uri)
    {
        return GetLength(uri).Map(length => GetData(uri, length));
    }

    private static IData GetData(Uri uri, long length)
    {
        return Data.FromStream(() =>
        {
            var httpClient = HttpClientFactory.CreateClient();
            return httpClient.GetStreamAsync(uri);
        }, length);
    }

    private static Task<Result<long>> GetLength(Uri uri)
    {
        return Result.Try(() => GetHeader(uri))
            .Bind(message => MaybeEx.FromNullableStruct(message.Content.Headers.ContentLength).ToResult("Could not determine the Content Length"));
    }

    private static async Task<HttpResponseMessage> GetHeader(Uri uri)
    {
        using var httpClient = HttpClientFactory.CreateClient();
        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Head, uri);
        return await httpClient.SendAsync(httpRequestMessage);
    }
}