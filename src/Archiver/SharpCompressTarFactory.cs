using SharpCompress.Archives.Tar;

namespace DotnetPackaging;

public class SharpCompressTarFactory : ITarFactory
{
    public ITarFile Create() => new SharcompressTarFile(TarArchive.Create());
}