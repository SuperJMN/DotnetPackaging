using System.IO.Abstractions;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using NyaFs.Filesystem.SquashFs;
using NyaFs.Filesystem.SquashFs.Types;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Comparer;
using Zafiro.FileSystem.Local;
using Zafiro.Reactive;

namespace DotnetPackaging.AppImage.Tests
{
    public static class Mixin
    {
        public static Result<IEnumerable<TResult>> MapMany<TInput, TResult>(
            this Result<IEnumerable<TInput>> taskResult,
            Func<TInput, TResult> selector)
        {
            return taskResult.Map((Func<IEnumerable<TInput>, IEnumerable<TResult>>)(inputs => inputs.Select<TInput, TResult>(selector)));
        }

        public static async Task<Result<IEnumerable<TResult>>> MapAndCombine<TInput, TResult>(
            this Result<IEnumerable<Task<Result<TInput>>>> enumerableOfTaskResults,
            Func<TInput, TResult> selector)
        {
            var result = await enumerableOfTaskResults.Map(async taskResults =>
            {
                var p = await Task.WhenAll(taskResults).ConfigureAwait(false);
                return p.Select(x => x.Map(selector)).Combine();
            }).ConfigureAwait(false);

            return result;
        }
    }

    public class UnitTest1
    {
        [Fact]
        public async Task Task()
        {
            var p = new NyaFs.Filesystem.SquashFs.SquashFsBuilder(SqCompressionType.Gzip);

            var fs = new FileSystemRoot(new ObservableFileSystem(new WindowsZafiroFileSystem(new FileSystem())));
            var root = fs.GetDirectory("c:/users/jmn/Desktop/Pack");

            var allFiles = await root.GetFilesInTree()
                .Bind(files => CreateDirs(files, root, p))
                .Bind(files => CreateFiles(files, root, p));

            var list = allFiles.Value.ToList();

            //p.Directory("/", 0, 0, 511);
            //p.Directory("/usr", 0, 0, 511);
            //p.Directory("/usr/bin", 0, 0, 511);
            //p.Directory("/usr/bin/AvaloniaSyncer", 0, 0, 511);

            //p.File(@"/AppRun", File.ReadAllBytes("c:/users/jmn/Desktop/AvaloniaSyncer.AppDir/AppRun"), 0, 0, 493);
            //p.File(@"/AvaloniaSyncer.desktop", File.ReadAllBytes("c:/users/jmn/Desktop/AvaloniaSyncer.AppDir/AvaloniaSyncer.desktop"), 0, 0, 493);
            //p.File(@"/AvaloniaSyncer.png", File.ReadAllBytes("c:/users/jmn/Desktop/AvaloniaSyncer.AppDir/AvaloniaSyncer.png"), 0, 0, 493);
            //p.File(@"/usr/bin/AvaloniaSyncer/Avalonia.Base.dll", File.ReadAllBytes("c:/users/jmn/Desktop/AvaloniaSyncer.AppDir/usr/bin/AvaloniaSyncer/Avalonia.Base.dll"), 0, 0, 493);
            //p.File(@"/usr/bin/AvaloniaSyncer/Avalonia.Controls.DataGrid.dll", File.ReadAllBytes("c:/users/jmn/Desktop/AvaloniaSyncer.AppDir/usr/bin/AvaloniaSyncer/Avalonia.Controls.DataGrid.dll"), 0, 0, 493);

            await File.WriteAllBytesAsync("c:\\users\\jmn\\Desktop\\Test.squashfs", p.GetFilesystemImage());
        }

        private async Task<Result<IEnumerable<IZafiroFile>>> CreateFiles(IEnumerable<IZafiroFile> files, IZafiroDirectory root, SquashFsBuilder squashFsBuilder)
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

            return r.Map(ints => files);
        }

        private Result<IEnumerable<IZafiroFile>> CreateDirs(IEnumerable<IZafiroFile> files, IZafiroDirectory root, SquashFsBuilder fs)
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

        [Fact]
        public async Task Test1()
        {
            var fs = new FileSystemRoot(new ObservableFileSystem(new WindowsZafiroFileSystem(new FileSystem())));
            var dir = fs.GetDirectory("C:/Users/JMN/Desktop/AvaloniaSyncer.AppDir");
            var mem = new MemoryStream();
            await AppImagePackager.CreatePayload(mem, dir);
            await File.WriteAllBytesAsync("Salida.squashfs", mem.ToArray());
        }

        [Fact]
        public async Task GetRuntime()
        {
            var stream = await RuntimeDownloader.GetRuntimeStream(Architecture.X64, new DefaultHttpClientFactory());
            var fs = File.OpenWrite("Runtime");
            await stream.CopyToAsync(fs);
        }

        [Fact]
        public async Task FullTest()
        {
            var runtimeStream = await RuntimeDownloader.GetRuntimeStream(Architecture.X64, new DefaultHttpClientFactory());
            var fs = new FileSystemRoot(new ObservableFileSystem(new WindowsZafiroFileSystem(new FileSystem())));
            var dir = fs.GetDirectory("C:/Users/JMN/Desktop/AvaloniaSyncer.AppDir");

            var payloadStream = new MemoryStream();
            await AppImagePackager.CreatePayload(payloadStream, dir);
            await File.WriteAllBytesAsync("Salida.squashfs", payloadStream.ToArray());

            // Concatenate runtimeStream and payloadStream
            var concatenatedStream = new MemoryStream();

            payloadStream.Position = 0;

            await runtimeStream.CopyToAsync(concatenatedStream);
            await payloadStream.CopyToAsync(concatenatedStream);
            // Write a file named "output.AppImage" with the resulting stream
            await File.WriteAllBytesAsync("output.AppImage", concatenatedStream.ToArray());
        }
    }
}