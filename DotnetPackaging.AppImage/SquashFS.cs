using ClassLibrary1;
using CSharpFunctionalExtensions;
using NyaFs.Filesystem.SquashFs;
using NyaFs.Filesystem.SquashFs.Types;
using NyaFs.Filesystem.Universal;
using System;
using MoreLinq;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.Reactive;
using ReactiveUI;

namespace DotnetPackaging.AppImage;

public static class SquashFS
{
    public async static Task<Result<Stream>> Build(IDirectory directory)
    {
        var builder = new SquashFsBuilder(SqCompressionType.Gzip);

        await directory.GetFilesInTree()
            .Check(files => CreateDirs(files, directory, builder))
            .Check(files => CreateFiles(files, directory, builder));

        return new MemoryStream(builder.GetFilesystemImage());
    }

    private static async Task<Result> CreateFiles(IEnumerable<IFile> files, IDirectory root, SquashFsBuilder squashFsBuilder)
    {
        var resultFiles = await files.Combine(file => file.ToBytes().Combine(file.GetPath()));
        var dataPathAndRootPath = await resultFiles.Bind(dataAndPath => root.GetPath().Map(rootPath => (dataAndPath, rootPath)));
        dataPathAndRootPath.Tap(contentFiles =>
        {
            AddFiles(contentFiles, squashFsBuilder);
        });

        return dataPathAndRootPath.Map(_ => Result.Success());
    }

    private static void AddFiles((IEnumerable<(byte[], ZafiroPath)> dataAndPath, ZafiroPath rootPath) contentFiles, SquashFsBuilder squashFsBuilder)
    {
        foreach (var contentFile in contentFiles.dataAndPath)
        {
            squashFsBuilder.File("/" + contentFile.Item2.MakeRelativeTo(contentFiles.rootPath), contentFile.Item1, 0, 0, 491);
        }
    }

    private static async Task<Result> CreateDirs(IEnumerable<IFile> files, IDirectory root, IFilesystemBuilder fs)
    {
        // Falta crear los directorios
        // Antes se hacía así
        //var paths = files
        //    .SelectMany(x => x.Path.MakeRelativeTo(root.Path).Parents())
        //    .Concat(new[] { ZafiroPath.Empty, })
        //    .Distinct()
        //    .OrderBy(path => path.RouteFragments.Count());
        throw new NotImplementedException();
    }
}

public static class ResultMixin
{
    public static async Task<Result<IEnumerable<TResult>>> Combine<T, TResult>(this IEnumerable<T> enumerable, Func<T, Task<Result<TResult>>> selector)
    {
        var whenAll = await Task.WhenAll(enumerable.Select(selector));
        return whenAll.Combine();
    }

    public static Task<Result<(T, Q)>> Combine<T, Q>(
        this Task<Result<T>> one,
        Task<Result<Q>> another)
    {
        return one.Bind(x => another.Map(y => (x, y)));
    }
}