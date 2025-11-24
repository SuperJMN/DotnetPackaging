using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Deb;
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
            .Map(deb => deb.ToData())
            .Bind(data => outputFile.SetContents(data));
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
