﻿using System.Reactive.Linq;
using DotnetPackaging.Deb;
using Zafiro.FileSystem;

namespace DotnetPackaging.Common;

public static class DumpMixin
{
    public static async Task DumpTo(this IEnumerable<byte> bytes, string path)
    {
        await using var stream = File.Create(path);
        await stream.WriteAsync(bytes.ToArray());
    }

    public static async Task DumpTo(this IObservable<byte> bytes, string path)
    {
        await using var stream = File.Create(path);
        await stream.WriteAsync(bytes.ToEnumerable().ToArray());
    }

    public static async Task DumpTo(this IObservable<byte> bytes, IZafiroFile file)
    {
        await using var memoryStream = new MemoryStream(bytes.ToEnumerable().ToArray());
        await file.SetContents(memoryStream);
    }
}