using MoreLinq;
using NyaFs.Filesystem.SquashFs;
using NyaFs.Filesystem.SquashFs.Types;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;
using UnixFile = Zafiro.DivineBytes.Unix.UnixFile;

namespace DotnetPackaging.AppImage.WIP;

public class SquashFS
{
    public static Result<IByteSource> Create(UnixDirectory directory)
    {
        var builder = new SquashFsBuilder(SqCompressionType.Gzip);
        return Result
            .Try(() => CreateRecursive(directory, "", builder))
            .MapTry(() => builder.GetFilesystemImage())
            .Map(bytes => ByteSource.FromBytes(bytes));
    }

    public static void CreateRecursive(UnixDirectory unixDir, string currentPath, SquashFsBuilder builder)
    {
        CreateDir(unixDir, currentPath, builder);;
        
        foreach (var subDir in unixDir.Subdirectories)
        {
            CreateDir(subDir, currentPath, builder);
        }
        
        foreach (var file in unixDir.Files)
        {
            CreateFile(file, currentPath, builder);
        }
    }

    private static void CreateFile(UnixFile unixFile, string currentPath, SquashFsBuilder builder)
    {
        var content = unixFile.Bytes.Array();
        builder.File(currentPath + "/" + unixFile.Name, content, (uint)unixFile.OwnerId, (uint)unixFile.OwnerId, GetFileMode(unixFile.Permissions));
    }

    private static uint GetFileMode(UnixPermissions unixFilePermissions)
    {
        uint mode = 0;

        // Owner
        if (unixFilePermissions.OwnerRead)  mode |= 0b100_000_000; // 0o400
        if (unixFilePermissions.OwnerWrite) mode |= 0b010_000_000; // 0o200
        if (unixFilePermissions.OwnerExec)  mode |= 0b001_000_000; // 0o100

        // Group
        if (unixFilePermissions.GroupRead)  mode |= 0b000_100_000; // 0o040
        if (unixFilePermissions.GroupWrite) mode |= 0b000_010_000; // 0o020
        if (unixFilePermissions.GroupExec)  mode |= 0b000_001_000; // 0o010

        // Others
        if (unixFilePermissions.OtherRead)  mode |= 0b000_000_100; // 0o004
        if (unixFilePermissions.OtherWrite) mode |= 0b000_000_010; // 0o002
        if (unixFilePermissions.OtherExec)  mode |= 0b000_000_001; // 0o001

        return mode;
    }

    private static void CreateDir(UnixDirectory unixDir, string currentPath, SquashFsBuilder builder)
    {
        var unixDirName = currentPath + "/" + unixDir.Name;
        builder.Directory(unixDirName, (uint)unixDir.OwnerId, (uint)unixDir.OwnerId, GetFileMode(unixDir.Permissions));
        unixDir.Subdirectories.ForEach(node => CreateDir(node, unixDirName, builder));
    }
}