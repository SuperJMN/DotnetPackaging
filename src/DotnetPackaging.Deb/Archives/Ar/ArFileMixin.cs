using System.Text;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class ArFileMixin
{
    public static IObservableDataStream ToByteProvider(this ArFile arFile)
    {
        return new CompositeObservableDataStream(Signature(), new CompositeObservableDataStream(arFile.Entries.Select(x => x.ToByteProvider()).ToArray()));
    }

    private static IObservableDataStream Signature()
    {
        return new StringObservableDataStream("!<arch>\n", Encoding.ASCII);
    }
}