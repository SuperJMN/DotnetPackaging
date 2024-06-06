using System.Threading.Tasks;
using DotnetPackaging.Deb.Archives.Deb;
using Zafiro.FileSystem.Mutable;

namespace DotnetPackaging.Gui.Core;

public class DebImagePackager : IPackager
{
    public string Name => "Debian .deb";

    public string Extension => ".deb";

    public Task<Result> CreatePackage(IDirectory sourceDirectory, IMutableFile outputFile, Options options)
    {
        return Deb.DebFile
            .From()
            .Directory(sourceDirectory)
            .Configure(x => x.From(options)).Build()
            .Map(deb => deb.ToData())
            .Bind(outputFile.SetContents);
    }
}