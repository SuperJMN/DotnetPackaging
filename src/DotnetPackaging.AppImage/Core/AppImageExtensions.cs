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
        var appImageResult = SquashFS.Create(appImageContainer.Container).Map(sqfs =>
        {
            var runtimeBytes = appImageContainer.Runtime.Array();
            var squashBytes = sqfs.Array();

            var i = ordinal++;
            var debug = Environment.GetEnvironmentVariable("DOTNETPACKAGING_DEBUG");
            if (!string.IsNullOrEmpty(debug) && (debug == "1" || debug.Equals("true", StringComparison.OrdinalIgnoreCase)))
            {
                var tempDir = System.IO.Path.GetTempPath();
                File.WriteAllBytes(System.IO.Path.Combine(tempDir, $"Runtime{i}.runtime"), runtimeBytes);
                File.WriteAllBytes(System.IO.Path.Combine(tempDir, $"Image{i}-Container.sqfs"), squashBytes);
            }

            var combined = new byte[runtimeBytes.Length + squashBytes.Length];
            Buffer.BlockCopy(runtimeBytes, 0, combined, 0, runtimeBytes.Length);
            Buffer.BlockCopy(squashBytes, 0, combined, runtimeBytes.Length, squashBytes.Length);

            return (IByteSource)ByteSource.FromBytes(combined);
        });

        return Task.FromResult(appImageResult);
    }
}
