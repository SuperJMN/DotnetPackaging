using System.Reactive.Linq;
using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Core;

public static class AppImageExtensions
{
    public static Task<Result<IByteSource>> ToByteSource(this AppImageContainer appImageContainer)
    {
        var appImageResult = SquashFS.Create(appImageContainer.Container).Map(squashFs =>
        {
            var concatenated = appImageContainer.Runtime.Bytes.Concat(squashFs.Bytes);
            return (IByteSource)ByteSource.FromByteObservable(concatenated);
        });

        return Task.FromResult(appImageResult);
    }
}
