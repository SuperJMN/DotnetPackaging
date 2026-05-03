using NyaFs.Filesystem.SquashFs;
using NyaFs.Filesystem.SquashFs.Types;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;
using UnixFile = Zafiro.DivineBytes.Unix.UnixFile;

namespace DotnetPackaging.AppImage.Core;

internal static class SquashFS
{
    public static async Task<Result<IByteSource>> Create(UnixDirectory container)
    {
        var builder = new SquashFsBuilder(SqCompressionType.Gzip);
        var created = await CreateRecursive(container, "", builder).ConfigureAwait(false);
        if (created.IsFailure)
        {
            return Result.Failure<IByteSource>(created.Error);
        }

        return Result
            .Try(builder.GetFilesystemImage)
            .Map(bytes => ByteSource.FromBytes(bytes).WithLength(bytes.LongLength));
    }

    private static async Task<Result> CreateRecursive(UnixDirectory unixDir, string currentPath, SquashFsBuilder builder)
    {
        var createdDirectory = Result.Try(() =>
        {
            // Always create the directory, including root directory
            if (string.IsNullOrEmpty(unixDir.Name))
            {
                // This is the root directory
                builder.Directory("/", (uint)unixDir.OwnerId, (uint)unixDir.OwnerId, GetFileMode(unixDir.Permissions));
                return "";
            }

            // Regular directory
            var dirPath = string.IsNullOrEmpty(currentPath) ? unixDir.Name : currentPath + "/" + unixDir.Name;
            builder.Directory(dirPath, (uint)unixDir.OwnerId, (uint)unixDir.OwnerId, GetFileMode(unixDir.Permissions));
            return dirPath;
        });

        if (createdDirectory.IsFailure)
        {
            return Result.Failure(createdDirectory.Error);
        }

        currentPath = createdDirectory.Value;

        // Create all files in the current directory
        foreach (var file in unixDir.Files)
        {
            var createdFile = await CreateFile(file, currentPath, builder).ConfigureAwait(false);
            if (createdFile.IsFailure)
            {
                return createdFile;
            }
        }

        // Recursively create subdirectories
        foreach (var subDir in unixDir.Subdirectories)
        {
            var createdSubdirectory = await CreateRecursive(subDir, currentPath, builder).ConfigureAwait(false);
            if (createdSubdirectory.IsFailure)
            {
                return createdSubdirectory;
            }
        }

        return Result.Success();
    }

    private static async Task<Result> CreateFile(UnixFile unixFile, string currentPath, SquashFsBuilder builder)
    {
        var content = await unixFile.ReadAll().ConfigureAwait(false);
        if (content.IsFailure)
        {
            return Result.Failure($"Could not read AppImage entry '{unixFile.Name}': {content.Error}");
        }

        return Result.Try(() =>
        {
            var filePath = string.IsNullOrEmpty(currentPath) ? unixFile.Name : currentPath + "/" + unixFile.Name;
            builder.File(filePath, content.Value, (uint)unixFile.OwnerId, (uint)unixFile.OwnerId, GetFileMode(unixFile.Permissions));
        });
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
