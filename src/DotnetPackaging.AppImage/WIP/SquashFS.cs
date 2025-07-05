using NyaFs.Filesystem.SquashFs;
using NyaFs.Filesystem.SquashFs.Types;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;
using UnixFile = Zafiro.DivineBytes.Unix.UnixFile;

namespace DotnetPackaging.AppImage.WIP;

public static class SquashFS
{
    public static Result<IByteSource> Create(UnixDirectory container)
    {
        var builder = new SquashFsBuilder(SqCompressionType.Gzip);
        return Result
            .Try(() => CreateRecursive(container, "", builder))
            .MapTry(() => builder.GetFilesystemImage())
            .Map(bytes => ByteSource.FromBytes(bytes));
    }

    public static void CreateRecursive(UnixDirectory unixDir, string currentPath, SquashFsBuilder builder)
    {
        // Always create the directory, including root directory
        string dirPath;
        if (string.IsNullOrEmpty(unixDir.Name))
        {
            // This is the root directory
            dirPath = "/";
            builder.Directory(dirPath, (uint)unixDir.OwnerId, (uint)unixDir.OwnerId, GetFileMode(unixDir.Permissions));
            currentPath = "";
        }
        else
        {
            // Regular directory
            dirPath = string.IsNullOrEmpty(currentPath) ? unixDir.Name : currentPath + "/" + unixDir.Name;
            builder.Directory(dirPath, (uint)unixDir.OwnerId, (uint)unixDir.OwnerId, GetFileMode(unixDir.Permissions));
            currentPath = dirPath;
        }

        // Create all files in the current directory
        foreach (var file in unixDir.Files)
        {
            CreateFile(file, currentPath, builder);
        }

        // Recursively create subdirectories
        foreach (var subDir in unixDir.Subdirectories)
        {
            CreateRecursive(subDir, currentPath, builder);
        }
    }

    private static void CreateFile(UnixFile unixFile, string currentPath, SquashFsBuilder builder)
    {
        var filePath = string.IsNullOrEmpty(currentPath) ? unixFile.Name : currentPath + "/" + unixFile.Name;
        var content = unixFile.Bytes.Array();
        builder.File(filePath, content, (uint)unixFile.OwnerId, (uint)unixFile.OwnerId, GetFileMode(unixFile.Permissions));
    }

    private static uint GetFileMode(UnixPermissions unixFilePermissions)
    {
        uint mode = 0;

        // Owner
        if (unixFilePermissions.OwnerRead) mode |= 0b100_000_000; // 0o400
        if (unixFilePermissions.OwnerWrite) mode |= 0b010_000_000; // 0o200
        if (unixFilePermissions.OwnerExec) mode |= 0b001_000_000; // 0o100

        // Group
        if (unixFilePermissions.GroupRead) mode |= 0b000_100_000; // 0o040
        if (unixFilePermissions.GroupWrite) mode |= 0b000_010_000; // 0o020
        if (unixFilePermissions.GroupExec) mode |= 0b000_001_000; // 0o010

        // Others
        if (unixFilePermissions.OtherRead) mode |= 0b000_000_100; // 0o004
        if (unixFilePermissions.OtherWrite) mode |= 0b000_000_010; // 0o002
        if (unixFilePermissions.OtherExec) mode |= 0b000_000_001; // 0o001

        return mode;
    }
}