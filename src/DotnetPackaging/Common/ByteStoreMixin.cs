using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.IO;

namespace DotnetPackaging.Common;

public static class ByteStoreMixin
{
    public static ByteStore ToByteStore(this FileInfo fileInfo)
    {
        return new ByteStore(Observable.Using(fileInfo.OpenRead, fileStream => fileStream.ToObservable()), fileInfo.Length);
    }

    public static async Task<Result<ByteStore>> ToByteStore(this IZafiroFile file)
    {
        var byteStore = await file.Size()
            .Bind(length => file.GetContents().Map(stream => (Stream: stream, Length: length)))
            .Map(tuple => new ByteStore(Observable.Using(() => tuple.Stream, fileStream => fileStream.ToObservable()), tuple.Length));

        return byteStore;
    }

    public static ByteStore ToByteStore(this string str)
    {
        return new ByteStore(str.GetAsciiBytes().ToObservable(), str.Length);
    }
}