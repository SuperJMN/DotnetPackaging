using System.Text;
using DotnetPackaging.Deb.Bytes;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class ArFileMixin
{
    public static IData ToData(this ArFile arFile)
    {
        return new CompositeData(Signature(), new CompositeData(arFile.Entries.Select(x => x.ToData()).ToArray()));
    }

    private static IData Signature()
    {
        return Data.FromString("!<arch>\n", Encoding.ASCII);
    }
}
