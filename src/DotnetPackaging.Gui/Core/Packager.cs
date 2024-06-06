using System.Threading.Tasks;
using Zafiro.FileSystem.Mutable;

namespace DotnetPackaging.Gui.Core;

public interface IPackager
{
    public string Name { get; }
    public string Extension { get; }
    public Task<Result> CreatePackage(IDirectory sourceDirectory, IMutableFile outputFile, Options options);
}