using System.Reactive.Linq;
using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Core;

public static class AppImageExtensions
{
    private static int ordinal;

    public static Task<Result<IByteSource>> ToByteSource(this AppImageContainer appImageContainer)
    {
        return SquashFS.Create(appImageContainer.Container).Map(async sqfs =>
        {
            var appImageBytes = appImageContainer.Runtime.Concat(sqfs);

            var i = ordinal++;
            await appImageContainer.Runtime.WriteTo($"/home/jmn/Escritorio/Runtime{i}-{appImageContainer.Runtime}.runtime").ConfigureAwait(false);
            await sqfs.WriteTo($"/home/jmn/Escritorio/Image{i}-Container.sqfs").ConfigureAwait(false);
            var fromByteChunks = ByteSource.FromByteChunks(appImageBytes);
            var totalBytes = fromByteChunks.Array();
            return fromByteChunks;
        });
    }
}