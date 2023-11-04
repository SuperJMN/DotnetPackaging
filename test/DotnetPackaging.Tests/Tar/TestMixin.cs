using System.Reactive.Linq;

namespace DotnetPackaging.Tests.Tar;

public static class TestMixin
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
}