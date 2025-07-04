using System.Reactive.Linq;
using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.WIP;

public static class AppImageExtensions
{
    public static Result<IByteSource> ToByteSource(this AppImage appImage)
    {
        return SquashFS.Create(appImage.Directory).Map(sqfs =>
        {
            var bytes = sqfs.Bytes.Array();
            var concat = appImage.Runtime.Concat(sqfs);
            return ByteSource.FromByteChunks(concat);
        });
    }
}