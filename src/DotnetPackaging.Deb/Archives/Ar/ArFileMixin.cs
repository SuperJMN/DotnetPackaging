using System.Text;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class ArFileMixin
{
    public static IByteProvider ToByteProvider(this ArFile arFile)
    {
        return new ComposedByteProvider(Signature(), new ComposedByteProvider(arFile.Entries.Select(x => x.ToByteProvider()).ToArray()));
    }

    private static IByteProvider Signature()
    {
        return new StringByteProvider("!<arch>\n", Encoding.ASCII);
    }
}