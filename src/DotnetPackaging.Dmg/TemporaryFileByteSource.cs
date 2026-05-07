using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Dmg;

internal static class TemporaryFileByteSource
{
    public static IByteSource OpenReadAndDelete(string path)
    {
        var length = new FileInfo(path).Length;
        var knownLength = Maybe.From(length);
        var source = ByteSource.FromStreamFactory(
            () => File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read),
            knownLength);

        return ByteSource.FromByteObservable(
            source.Bytes.Finally(() => TryDelete(path)),
            knownLength);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
