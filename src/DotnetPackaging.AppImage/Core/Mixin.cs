using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public static class Mixin
{
    public static Task<LinuxFileEntry[]> ToLinuxFileEntries(this IEnumerable<RootedFile> toLinuxEntries)
    {
        return Task.WhenAll(toLinuxEntries.Select(async r =>
        {
            var unixFileMode = await GetMode(r);
            var fullPath = r.Path == string.Empty ? r.File.Name : r.Path + "/" + r.File.Name;
            return new LinuxFileEntry(fullPath, r.File, "", "", unixFileMode);
        }));
    }

    public static Task<Result<bool>> IsExecutable(this RootedFile entry)
    {
        return entry.File.Within(stream =>
        {
            return stream.IsElf().Map(isElf => entry.File.Name == "AppRun" || (isElf && !entry.File.Name.EndsWith(".so") && entry.File.Name != "createdump"));
        });
    }
    
    private static Task<UnixFileMode> GetMode(RootedFile valueTuple)
    {
        const UnixFileMode execFile = (UnixFileMode) 755;
        const UnixFileMode regularFile = (UnixFileMode) 544;

        return valueTuple.IsExecutable().Map(isExec => isExec ? execFile : regularFile).GetValueOrDefault(() => regularFile);
    }
}