using Zafiro.DataModel;

namespace DotnetPackaging.Rpm;

public static class RpmPackageExtensions
{
    public static IData ToData(this RpmPackage package)
    {
        return RpmArchiveWriter.Write(package);
    }
}
