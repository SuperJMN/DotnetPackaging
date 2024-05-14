using System.Text;
using Zafiro.DataModel;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class ArFileMixin
{
    public static IData ToData(this ArFile arFile)
    {
        return new CompositeData(Signature(), new CompositeData(arFile.Entries.Select(x => x.ToData()).ToArray()));
    }

    private static IData Signature()
    {
        return new StringData("!<arch>\n", Encoding.ASCII);
    }
}