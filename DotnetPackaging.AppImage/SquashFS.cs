using CSharpFunctionalExtensions;
using NyaFs.Filesystem.SquashFs;
using NyaFs.Filesystem.SquashFs.Types;
using NyaFs.Filesystem.Universal;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.Reactive;

namespace DotnetPackaging.AppImage;

public static class SquashFS
{
    public async static Task<Result<Stream>>  Build(IZafiroDirectory directory)
    {
        var builder = new SquashFsBuilder(SqCompressionType.Gzip);

        await directory.GetFilesInTree()
            .Bind(files => CreateDirs(files, directory, builder))
            .Bind(files => CreateFiles(files, directory, builder));

        return new MemoryStream(builder.GetFilesystemImage());
    }
        
    private static async Task<Result<IEnumerable<IZafiroFile>>> CreateFiles(IEnumerable<IZafiroFile> files, IZafiroDirectory root, SquashFsBuilder squashFsBuilder)
    {
        var p = Result.Success()
            .Map(() => files.Select(file => file.GetData().Map(async stream =>
            {
                using (stream)
                {
                    return (Contents: await stream.ReadBytes(), Path: file.Path);
                }
            })));

        var r = await p.MapAndCombine(tuple =>
        {
            if (tuple.Contents.Length == 0)
            {
                return 1;
            }
            squashFsBuilder.File("/" + tuple.Path.MakeRelativeTo(root.Path), tuple.Contents, 0, 0, 491);
            return 1;
        });

        return r.Map(_ => files);
    }

    private static Result<IEnumerable<IZafiroFile>> CreateDirs(IEnumerable<IZafiroFile> files, IZafiroDirectory root, IFilesystemBuilder fs)
    {
        return Result.Try(() =>
        {
            var paths = files
                .SelectMany(x => x.Path.MakeRelativeTo(root.Path).Parents())
                .Concat(new[] { ZafiroPath.Empty, })
                .Distinct()
                .OrderBy(path => path.RouteFragments.Count());

            foreach (var zafiroPath in paths)
            {
                fs.Directory("/" + zafiroPath, 0, 0, 511);
            }

            return files;
        });
    }
}