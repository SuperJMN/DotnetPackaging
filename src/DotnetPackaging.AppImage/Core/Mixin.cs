using CSharpFunctionalExtensions;
using System.IO;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public static class Mixin
{
    public static Task<UnixFile[]> ToUnixFileList(this IEnumerable<RootedFile> toLinuxEntries)
    {
        return Task.WhenAll(toLinuxEntries.Select(async r =>
        {
            var unixFilePermissions = await GetMode(r);
            var fullPath = r.Path == string.Empty ? r.File.Name : r.Path + "/" + r.File.Name;
            return new UnixFile(fullPath, r, "", "", unixFilePermissions);
        }));
    }

    public static Task<Result<bool>> IsExecutable(this IRootedFile entry)
    {
        return Task.FromResult(entry.IsElf().Map(isElf => entry.Name == "AppRun" || (isElf && !entry.Name.EndsWith(".so") && entry.Name != "createdump")));
    }
    
    private static Task<UnixFilePermissions> GetMode(RootedFile valueTuple)
    {
        var execFile = (UnixFilePermissions) Convert.ToUInt32("755", 8);
        var regularFile = (UnixFilePermissions) Convert.ToUInt32("644", 8);

        return valueTuple.IsExecutable().Map(isExec => isExec ? execFile : regularFile).GetValueOrDefault(() => regularFile);
    }
}
