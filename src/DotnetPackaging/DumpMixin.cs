using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem;

namespace DotnetPackaging;

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

    public static async Task<Result> DumpTo(this IObservable<byte> bytes, IZafiroFile file)
    {
        return await file.SetContents(bytes);
    }
}