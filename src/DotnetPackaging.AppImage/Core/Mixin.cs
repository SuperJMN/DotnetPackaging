using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public static class Mixin
{
    public static Task<LinuxFileEntry[]> ToLinuxFileEntries(this IEnumerable<(ZafiroPath Path, IBlob Blob)> toLinuxEntries)
    {
        return Task.WhenAll(toLinuxEntries.Select(async r =>
        {
            var unixFileMode = await GetMode(r);
            return new LinuxFileEntry(r.Path, r.Blob, "", "", unixFileMode);
        }));
    }

    public static Task<Result<LinuxFileEntry[]>> ToLinuxFileEntries(this IBlobContainer toLinuxEntries)
    {
        return toLinuxEntries
            .GetBlobsInTree(ZafiroPath.Empty)
            .Map(entries => entries.ToLinuxFileEntries());
    }

    private static async Task<UnixFileMode> GetMode((ZafiroPath path, IBlob blob) valueTuple)
    {
        const UnixFileMode execFile = (UnixFileMode) 755;
        const UnixFileMode regularFile = (UnixFileMode) 544;
        
        if (valueTuple.path == "AppRun")
        {
            
            return execFile;
        }

        return await valueTuple.blob.StreamFactory()
            .Bind(stream =>
            {
                using (stream)
                {
                    return stream.IsExecutable().Map(isExec => isExec ? execFile : regularFile);
                }
            }).GetValueOrDefault(() => regularFile);
    }
}