using CSharpFunctionalExtensions;
using NyaFs.Filesystem.SquashFs;
using NyaFs.Filesystem.SquashFs.Types;
using NyaFs.Filesystem.Universal;
using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage.Core;

public static class SquashFS
{
    public static Task<Result> Write(Stream stream, IList<UnixFile> fileEntries)
    {
        var builder = new SquashFsBuilder(SqCompressionType.Gzip);

        return CreateDirs(fileEntries, builder)
            .Check(_ => CreateFiles(fileEntries, builder))
            .Bind(_ => Result.Try(() => stream.Write(builder.GetFilesystemImage())));
    }

    private static async Task<Result> CreateFiles(IList<UnixFile> files, SquashFsBuilder squashFsBuilder)
    {
        var resultFiles = await GetFilesAndContents(files);
        resultFiles.Tap(contentFiles => AddFiles(contentFiles, squashFsBuilder));
        return resultFiles.Map(_ => Result.Success());
    }

    private static Task<Result<IEnumerable<(byte[] bytes, ZafiroPath path, string owner, string group, UnixFilePermissions UnixFilePermissions)>>> GetFilesAndContents(IList<UnixFile> files)
    {
        return files.Combine(file => file.data.ToBytes().Map(bytes => (bytes, file.path, file.owner, file.group, file.UnixFilePermissions)));
    }

    private static void AddFiles(IEnumerable<(byte[] bytes, ZafiroPath path, string owner, string group, UnixFilePermissions UnixFilePermissions)> contentFiles, SquashFsBuilder squashFsBuilder)
    {
        foreach (var contentFile in contentFiles)
        {
            squashFsBuilder.File("/" + contentFile.Item2, contentFile.Item1, 0, 0, (uint)contentFile.UnixFilePermissions);
        }
    }

 
    private static Result<IList<UnixFile>> CreateDirs(IList<UnixFile> files, IFilesystemBuilder fs)
    {
        var paths = GetDirectoryPaths(files);
        AddDirectories(paths, fs);
        return Result.Success(files);
    }

    private static Result<IEnumerable<ZafiroPath>> AddDirectories(IEnumerable<ZafiroPath> paths, IFilesystemBuilder fs)
    {
        var mode = Convert.ToUInt32("755", 8);
        foreach (var path in paths)
        {
            fs.Directory("/" + path, 0, 0, mode);
        }

        return Result.Success(paths);
    }

    private static IEnumerable<ZafiroPath> GetDirectoryPaths(IList<UnixFile> files)
    {
        return files
            .SelectMany(x => x.path.Parents())
            .Concat(new[] { ZafiroPath.Empty })
            .Distinct()
            .OrderBy(path => path.RouteFragments.Count());
    }
}