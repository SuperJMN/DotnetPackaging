using MoreLinq;
using NyaFs.Filesystem.SquashFs;
using NyaFs.Filesystem.SquashFs.Types;
using Zafiro.DataModel;
using Zafiro.FileSystem.Core;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.AppImage.Core;

public class SquashFS
{
    public static Result<IData> Create(UnixRoot root)
    {
        var builder = new SquashFsBuilder(SqCompressionType.Gzip);
        return Result
            .Try(() => Create(root, "", builder))
            .MapTry(() => builder.GetFilesystemImage())
            .Map(bytes => (IData)Data.FromByteArray(bytes));
    }

    public static void Create(UnixNode unixDir, string currentPath, SquashFsBuilder builder)
    {
        switch (unixDir)
        {
            case UnixDir subdirectory:
                CreateDir(subdirectory, currentPath, builder);
                break;
            case UnixFile unixFile:
                CreateFile(unixFile, currentPath, builder);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(unixDir));
        }
    }

    private static void CreateFile(UnixFile unixFile, string currentPath, SquashFsBuilder builder)
    {
        var content = unixFile.Bytes();
        builder.File(currentPath + "/" + unixFile.Name, content, (uint)unixFile.Properties.OwnerId.GetValueOrDefault(), (uint)unixFile.Properties.OwnerId.GetValueOrDefault(), (uint)unixFile.Properties.FileMode);
    }

    private static void CreateDir(UnixDir unixDir, string currentPath, SquashFsBuilder builder)
    {
        var unixDirName = currentPath + "/" + unixDir.Name;
        builder.Directory(unixDirName, (uint)unixDir.Properties.OwnerId.GetValueOrDefault(), (uint)unixDir.Properties.OwnerId.GetValueOrDefault(), (uint)unixDir.Properties.FileMode);
        unixDir.Nodes.ForEach(node => Create(node, unixDirName, builder));
    }
}