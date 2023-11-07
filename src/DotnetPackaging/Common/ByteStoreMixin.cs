using System.Reactive.Linq;
using Zafiro.IO;

namespace DotnetPackaging.Common;

public static class ByteStoreMixin
{
    public static ByteStore ToByteStore(this FileInfo fileInfo)
    {
        return new ByteStore(Observable.Using(fileInfo.OpenRead, fileStream => fileStream.ToObservable()), fileInfo.Length);
    }
}