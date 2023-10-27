using CSharpFunctionalExtensions;
using SharpCompress.Archives.Tar;

namespace Archiver;

public interface ITarFactory
{
    public ITarFile Create();
}

public class SharpCompressTarFactory : ITarFactory
{
    public ITarFile Create() => new SharcompressTarFile(TarArchive.Create());
}