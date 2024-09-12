using System.Threading.Tasks;
using DotnetPackaging.AppImage.Core;
using Zafiro.FileSystem.Mutable;
using Zafiro.FileSystem.Readonly;

namespace DotnetPackaging.Gui.Core;

public class AppImagePackager : IPackager
{
    public string Name => "AppImage";

    public string Extension => ".AppImage";

    public Task<Result> CreatePackage(IDirectory sourceDirectory, IMutableFile outputFile, Options options)
    {
        return AppImage.AppImage
            .From()
            .Directory(sourceDirectory)
            .Configure(x => x.From(options)).Build()
            .Bind(appImage => appImage.ToData().Bind(data => outputFile.SetContents(data)));
    }
}