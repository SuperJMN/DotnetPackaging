using System.Reactive.Linq;
using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Core;

public static class AppImageExtensions
{
    public static Task<Result<IByteSource>> ToByteSource(this AppImageContainer appImageContainer)
    {
        return SquashFS.Create(appImageContainer.Container).Map(async sqfs =>
        {
            await sqfs.WriteTo($"/home/jmn/Escritorio/AppImageContents-{appImageContainer.Runtime.Architecture}.squashfs");
            var appImageBytes = appImageContainer.Runtime.Concat(sqfs);
            return ByteSource.FromByteChunks(appImageBytes);
        });
    }
}