using SharpCompress.Archives.Tar;

namespace Archiver;

public class SharpCompressTarFactory : ITarFactory
{
    public ITarFile Create() => new SharcompressTarFile(TarArchive.Create());
}