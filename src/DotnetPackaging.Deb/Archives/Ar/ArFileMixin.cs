using System.Linq;
using System.Reactive.Linq;
using System.Text;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class ArFileMixin
{
    public static IByteSource ToByteSource(this ArFile arFile)
    {
        var chunks = arFile.Entries
            .Select(entry => entry.ToObservable())
            .Aggregate(Observable.Return(Encoding.ASCII.GetBytes("!<arch>\n")), (current, next) => current.Concat(next));

        return ByteSource.FromByteObservable(chunks);
    }
}
