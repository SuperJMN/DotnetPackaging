using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
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

