using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DiscUtils.SquashFs;
using NyaFs.Filesystem.SquashFs.Types;
using Zafiro.FileSystem;
using Zafiro.Reactive;

namespace DotnetPackaging.AppImage;

public class AppImagePackager
{
    public static async Task CreatePayload(Stream output, IZafiroDirectory directory)
    {
        var results = await directory
            .GetFilesInTree()
            .Bind(files => Combine(directory, files));

        await results.Tap(async fileData =>
        {
            var fs = new NyaFs.Filesystem.SquashFs.SquashFsBuilder(SqCompressionType.Gzip);

            HashSet<ZafiroPath> dirs = new();
            foreach (var f in fileData.OrderBy(x => x.Path.RouteFragments.Count()))
            {
                var zafiroPath = f.Path.Parent();
                if (!dirs.Contains(zafiroPath))
                {
                    fs.Directory("/" + zafiroPath, 0,0, 511);
                    dirs.Add(zafiroPath);
                }
                fs.File("/" + f.Path, f.Bytes, 0, 0, 493);
            }

            await output.WriteAsync(fs.GetFilesystemImage());
        });
    }

    private static Task<Result<IEnumerable<(ZafiroPath Path, byte[] Bytes)>>> Combine(IZafiroDirectory directory, IEnumerable<IZafiroFile> files)
    {
        return files
            .Select(file => file.GetData()
                .Map(async stream => (Path: file.Path.MakeRelativeTo(directory.Path), Bytes: await stream.ReadBytes())))
            .Combine();
    }

    private static void AddFiles(IEnumerable<(ZafiroPath Path, byte[] Bytes)> valueTuples, SquashFileSystemBuilder fs)
    {
        foreach (var valueTuple in valueTuples.Where(tuple => tuple.Bytes.Length > 0).Take(5))
        {
            fs.AddFile(valueTuple.Path, valueTuple.Bytes);
        }
    }

    public static async Task Create(Stream input, Architecture arch, Stream payload)
    {
        var runtime = await GetRuntime(arch);
        await runtime.CopyToAsync(input);
        await runtime.CopyToAsync(payload);
    }

    private static async Task<Stream> GetRuntime(Architecture arch)
    {
        return await Download("https://github.com/AppImage/type2-runtime/releases/download/old/runtime-fuse2-aarch64");
    }

    private static async Task<Stream> Download(string url)
    {
        throw new NotImplementedException();
    }
}

public class DisposeAwareStream : Stream, IDisposable
{
    private readonly Stream stream;

    public DisposeAwareStream(Stream stream)
    {
        this.stream = stream;
    }

    public void Dispose()
    {
        IsDisposed = true;
        // TODO release managed resources here
    }

    public bool IsDisposed { get; set; }

    public override void Flush()
    {
        stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

    public override void SetLength(long value)
    {
        stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        stream.Write(buffer, offset, count);
    }

    public override bool CanRead => stream.CanRead;

    public override bool CanSeek => stream.CanSeek;

    public override bool CanWrite => stream.CanWrite;

    public override long Length => stream.Length;

    public override long Position
    {
        get => stream.Position;
        set => stream.Position = value;
    }
}

public static class Mixin
{
    public static Result<IEnumerable<TResult>> MapMany<TInput, TResult>(
        this Result<IEnumerable<TInput>> taskResult,
        Func<TInput, TResult> selector)
    {
        return taskResult.Map((Func<IEnumerable<TInput>, IEnumerable<TResult>>) (inputs => inputs.Select<TInput, TResult>(selector)));
    }
}