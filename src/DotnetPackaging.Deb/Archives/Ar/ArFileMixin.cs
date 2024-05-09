using System.Text;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class ArFileMixin
{
    public static IData ToByteProvider(this ArFile arFile)
    {
        return new CompositeData(Signature(), new CompositeData(arFile.Entries.Select(x => x.ToByteProvider()).ToArray()));
    }

    private static IData Signature()
    {
        return new StringData("!<arch>\n", Encoding.ASCII);
    }
}