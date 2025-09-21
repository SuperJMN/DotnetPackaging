using System;
using System.IO;
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
            var debug = Environment.GetEnvironmentVariable("DOTNETPACKAGING_DEBUG");
            if (!string.IsNullOrEmpty(debug) && (debug == "1" || debug.Equals("true", StringComparison.OrdinalIgnoreCase)))
            {
                var tempDir = System.IO.Path.GetTempPath();
                await appImageContainer.Runtime.WriteTo(System.IO.Path.Combine(tempDir, $"Runtime{i}.runtime")).ConfigureAwait(false);
                await sqfs.WriteTo(System.IO.Path.Combine(tempDir, $"Image{i}-Container.sqfs")).ConfigureAwait(false);
            }

            var fromByteChunks = ByteSource.FromByteChunks(appImageBytes);
            var totalBytes = fromByteChunks.Array();
            return fromByteChunks;
        });
    }
}
