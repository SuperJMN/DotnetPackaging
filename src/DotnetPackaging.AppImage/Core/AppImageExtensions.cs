using System.Reactive.Linq;
using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Core;

public static class AppImageExtensions
{
    public static Result<IByteSource> ToByteSource(this AppImageContainer appImageContainer)
    {
        return SquashFS.Create(appImageContainer.Container).Map(sqfs =>
        {
            var appImageBytes = appImageContainer.Runtime.Concat(sqfs);
            return ByteSource.FromByteChunks(appImageBytes);
        });
    }
}