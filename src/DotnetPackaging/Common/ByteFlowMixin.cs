using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.IO;

namespace DotnetPackaging.Common;

public static class ByteFlowMixin
{
    public static Task<Result<ByteFlow>> ToByteFlow(this IZafiroFile file)
    {
        return file.GetContents().CombineAndMap(file.Size(), (stream, l) => { return new ByteFlow(Observable.Using(() => stream, s => s.ToObservable()), l); });
    }

    public static ByteFlow ToByteFlow(this string str, Encoding encoding)
    {
        return new ByteFlow(encoding.GetBytes(str).ToObservable(), str.Length);
    }
}