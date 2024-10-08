﻿using Zafiro.DataModel;

namespace DotnetPackaging.AppImage.Core;

public class UriRuntime : IRuntime
{
    private readonly IData runtimeImplementation;

    private UriRuntime(IData data)
    {
        runtimeImplementation = data;
    }

    public static async Task<Result<UriRuntime>> Create(Uri uri)
    {
        var data = await HttpRequestData.Create(uri);
        return data.Map(data => new UriRuntime(data));
    }

    public IObservable<byte[]> Bytes => runtimeImplementation.Bytes;

    public long Length => runtimeImplementation.Length;
}