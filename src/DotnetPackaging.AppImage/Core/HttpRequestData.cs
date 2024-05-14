using System.Reactive.Linq;
using Zafiro.DataModel;

namespace DotnetPackaging.AppImage.Core;

public static class HttpRequestData
{
    private static readonly IHttpClientFactory HttpClientFactory = new DefaultHttpClientFactory();

    public static Task<Result<IData>> Create(Uri uri)
    {
        return Result
            .Try(() =>
            {
                var httpClient = HttpClientFactory.CreateClient();
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Head, uri);
                return httpClient.SendAsync(httpRequestMessage);
            })
            .Bind(message => ResultEx.FromNullableStruct(message.Content.Headers.ContentLength).ToResult("Could not determine the Content Length"))
            .Map(l =>
            {
                var usingAsync = Observable
                    .Using(
                        () => HttpClientFactory.CreateClient(),
                        client => ReactiveData.Chunked(() => client.GetStreamAsync(uri))
                    );
                return (IData)new Data(usingAsync, l);
            });
    }
}