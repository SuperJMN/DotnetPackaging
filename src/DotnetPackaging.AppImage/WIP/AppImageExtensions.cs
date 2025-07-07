using System.Reactive.Linq;
using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.WIP;

public static class AppImageExtensions
{
    public static Result<IByteSource> ToByteSource(this AppImage appImage)
    {
        return SquashFS.Create(appImage.Container).Map(sqfs =>
        {
            var appImageBytes = appImage.Runtime.Concat(sqfs);
            return ByteSource.FromByteChunks(appImageBytes);
        });
    }
}