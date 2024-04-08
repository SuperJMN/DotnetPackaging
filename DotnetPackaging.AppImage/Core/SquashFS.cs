using Zafiro.FileSystem.Lightweight;
using CSharpFunctionalExtensions;
using NyaFs.Filesystem.SquashFs;
using NyaFs.Filesystem.SquashFs.Types;
using NyaFs.Filesystem.Universal;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public static class SquashFS
{
    private static async Task<Result> CreateFiles(IEnumerable<(ZafiroPath, IBlob)> files, SquashFsBuilder squashFsBuilder)
    {
        var resultFiles = await GetFilesAndContents(files);
        resultFiles.Tap(contentFiles =>
        {
            AddFiles(contentFiles, squashFsBuilder);
        });

        return resultFiles.Map(_ => Result.Success());
    }

    private static async Task<Result<IEnumerable<(byte[] bytes, ZafiroPath)>>> GetFilesAndContents(IEnumerable<(ZafiroPath, IBlob)> files)
    {
        return await files.Combine(file => file.Item2.ToBytes().Map(bytes => (bytes, file.Item1)));
    }

    private static void AddFiles(IEnumerable<(byte[] bytes, ZafiroPath)> contentFiles, SquashFsBuilder squashFsBuilder)
    {
        uint mode = Convert.ToUInt32("644", 8);
        foreach (var contentFile in contentFiles)
        {
            squashFsBuilder.File("/" + contentFile.Item2, contentFile.Item1, 0, 0, mode);
        }
    }

    private static Result<IEnumerable<(ZafiroPath, IBlob)>> CreateDirs(IEnumerable<(ZafiroPath, IBlob)> files, IFilesystemBuilder fs)
    {
        var paths = GetDirectoryPaths(files);
        AddDirectories(paths, fs);
        return Result.Success(files);
    }

    private static Result<IEnumerable<ZafiroPath>> AddDirectories(IEnumerable<ZafiroPath> paths, IFilesystemBuilder fs)
    {
        uint mode = Convert.ToUInt32("755", 8);
        foreach (var path in paths)
        {
            fs.Directory("/" + path, 0, 0, mode);
        }

        return Result.Success(paths);
    }

    private static IEnumerable<ZafiroPath> GetDirectoryPaths(IEnumerable<(ZafiroPath, IBlob)> files)
    {
        return files
            .SelectMany(x => x.Item1.Parents())
            .Concat(new[] { ZafiroPath.Empty, })
            .Distinct()
            .OrderBy(path => path.RouteFragments.Count());
    }

    public static Task<Result> Write(Stream stream, IBlobContainer dataTree)
    {
        var builder = new SquashFsBuilder(SqCompressionType.Gzip);

        return dataTree.GetBlobsInTree(ZafiroPath.Empty)
            .Check(files => CreateDirs(files, builder))
            .Check(files => CreateFiles(files, builder))
            .Bind(_ => Result.Try(() => stream.Write(builder.GetFilesystemImage())));
    }
}