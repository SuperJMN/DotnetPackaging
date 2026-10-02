using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;
using UnixFile = Zafiro.DivineBytes.Unix.UnixFile;

namespace DotnetPackaging.AppImage.Core;

internal static class SquashFS
{
    public static async Task<Result<IByteSource>> Create(UnixDirectory container)
    {
        var root = await ToNode(container).ConfigureAwait(false);

        return root
            .Bind(directory => Result.Try(() => SquashFsImage.Create(directory, DateTimeOffset.UtcNow)))
            .Map(bytes => ByteSource.FromBytes(bytes).WithLength(bytes.LongLength));
    }

    private static async Task<Result<SquashFsDirectory>> ToNode(UnixDirectory unixDir)
    {
        var children = new List<SquashFsNode>();

        foreach (var file in unixDir.Files)
        {
            var created = await ToNode(file).ConfigureAwait(false);
            if (created.IsFailure)
            {
                return Result.Failure<SquashFsDirectory>(created.Error);
            }

            children.Add(created.Value);
        }

        foreach (var subDir in unixDir.Subdirectories)
        {
            var created = await ToNode(subDir).ConfigureAwait(false);
            if (created.IsFailure)
            {
                return created;
            }

            children.Add(created.Value);
        }

        return new SquashFsDirectory(unixDir.Name, GetFileMode(unixDir.Permissions), (uint)unixDir.OwnerId, children);
    }

    private static async Task<Result<SquashFsNode>> ToNode(UnixFile unixFile)
    {
        var content = await unixFile.ReadAll().ConfigureAwait(false);

        return content
            .MapError(error => $"Could not read AppImage entry '{unixFile.Name}': {error}")
            .Map(SquashFsNode (bytes) => new SquashFsFile(unixFile.Name, GetFileMode(unixFile.Permissions), (uint)unixFile.OwnerId, bytes));
    }

    private static ushort GetFileMode(UnixPermissions unixFilePermissions)
    {
        ushort mode = 0;

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
