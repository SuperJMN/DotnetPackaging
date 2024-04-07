using ClassLibrary1;
using CSharpFunctionalExtensions;
using NyaFs.Filesystem.SquashFs;
using NyaFs.Filesystem.SquashFs.Types;
using NyaFs.Filesystem.Universal;
using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage.Core;

public static class SquashFS
{
    public async static Task<Result<Stream>> Build(IDataTree dataTree)
    {
        var builder = new SquashFsBuilder(SqCompressionType.Gzip);

        await dataTree.GetFilesAndPaths()
            .Check(files => CreateDirs(files, builder))
            .Check(files => CreateFiles(files, builder));

        return new MemoryStream(builder.GetFilesystemImage());
    }

    private static async Task<Result> CreateFiles(IEnumerable<(IData, ZafiroPath)> files, SquashFsBuilder squashFsBuilder)
    {
        var resultFiles = await GetFilesAndContents(files);
        resultFiles.Tap(contentFiles =>
        {
            AddFiles(contentFiles, squashFsBuilder);
        });

        return resultFiles.Map(_ => Result.Success());
    }

    private static async Task<Result<IEnumerable<(byte[] bytes, ZafiroPath)>>> GetFilesAndContents(IEnumerable<(IData, ZafiroPath)> files)
    {
        return await files.Combine(file => file.Item1.ToBytes().Map(bytes => (bytes, file.Item2)));
    }

    private static void AddFiles(IEnumerable<(byte[] bytes, ZafiroPath)> contentFiles, SquashFsBuilder squashFsBuilder)
    {
        foreach (var contentFile in contentFiles)
        {
            squashFsBuilder.File("/" + contentFile.Item2, contentFile.Item1, 0, 0, 491);
        }
    }

    private static Result<IEnumerable<(IData, ZafiroPath)>> CreateDirs(IEnumerable<(IData, ZafiroPath)> files, IFilesystemBuilder fs)
    {
        var paths = GetDirectoryPaths(files);
        AddDirectories(paths, fs);
        return Result.Success(files);
    }

    private static Result<IEnumerable<ZafiroPath>> AddDirectories(IEnumerable<ZafiroPath> paths, IFilesystemBuilder fs)
    {
        foreach (var path in paths)
        {
            fs.Directory("/" + path, 0, 0, 491);
        }

        return Result.Success(paths);
    }

    private static IEnumerable<ZafiroPath> GetDirectoryPaths(IEnumerable<(IData, ZafiroPath)> files)
    {
        return files
            .SelectMany(x => x.Item2.Parents())
            .Concat(new[] { ZafiroPath.Empty, })
            .Distinct()
            .OrderBy(path => path.RouteFragments.Count());
    }

    public static Task<Result> Write(MemoryStream stream, IDataTree dataTree)
    {
        var builder = new SquashFsBuilder(SqCompressionType.Gzip);

        return dataTree.GetFilesAndPaths()
            .Check(files => CreateDirs(files, builder))
            .Check(files => CreateFiles(files, builder))
            .Bind(_ => Result.Try(() => stream.Write(builder.GetFilesystemImage())));
    }
}