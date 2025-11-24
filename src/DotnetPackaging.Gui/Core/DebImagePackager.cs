using System.Threading.Tasks;
using DotnetPackaging.Deb.Archives.Deb;
using Zafiro.DataModel;
using Zafiro.DivineBytes;
using Zafiro.FileSystem.Core;
using Zafiro.FileSystem.Mutable;
using Zafiro.FileSystem.Readonly;

namespace DotnetPackaging.Gui.Core;

public class DebImagePackager : IPackager
{
    public string Name => "Debian .deb";

    public string Extension => ".deb";

    public Task<Result> CreatePackage(IDirectory sourceDirectory, IMutableFile outputFile, Options options)
    {
        var containerResult = BuildContainer(sourceDirectory);
        if (containerResult.IsFailure)
        {
            return Task.FromResult(Result.Failure(containerResult.Error));
        }

        return Deb.DebFile
            .From()
            .Container(containerResult.Value, sourceDirectory.Name)
            .Configure(x => x.From(options)).Build()
            .Map(deb => deb.ToByteSource())
            .Bind(async data =>
            {
                var bytes = data.Array();
                var content = Data.FromByteArray(bytes);
                return await outputFile.SetContents(content);
            });
    }

    private static Result<RootContainer> BuildContainer(IDirectory root)
    {
        var files = root.RootedFiles()
            .ToDictionary(
                file => file.Path == ZafiroPath.Empty ? file.Name : file.Path.Combine(file.Name).ToString(),
                file => (IByteSource)ByteSource.FromByteObservable(file.Value.Bytes),
                StringComparer.Ordinal);

        return files.ToRootContainer();
    }
}
