using System.Reactive.Linq;
using Zafiro.DataModel;
using Zafiro.Reactive;

namespace DotnetPackaging.AppImage.Kernel;

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
            .Bind(message => MaybeMixin.FromNullableStruct(message.Content.Headers.ContentLength).ToResult("Could not determine the Content Length"))
            .Map(l =>
            {
                var usingAsync = Observable
                    .Using(
                        () => HttpClientFactory.CreateClient(),
                        client => ObservableFactory.UsingAsync(
                            () => client.GetStreamAsync(uri),
                            stream => stream.ToObservableChunked())
                    );

                var data = new Data(usingAsync, l);
                return (IData) data;
            });
    }
}