﻿using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Tests;
using Zafiro.FileSystem;
using Zafiro.Reactive;

namespace DotnetPackaging.AppImage;

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
            .Bind(message => MaybeMixin.FromNullableStruct(message.Content.Headers.ContentLength).ToResult("cannot"))
            .Map(l => (IData)new Data(ObservableFactory.UsingAsync(() => HttpClientFactory.CreateClient().GetStreamAsync(uri), stream => stream.ToObservableChunked()), l));
    }
}