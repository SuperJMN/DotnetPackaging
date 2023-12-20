using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem;

namespace DotnetPackaging.Common;

public static class ByteFlowMixin
{
    public static Task<Result<ByteFlow>> ToByteFlow(this IZafiroFile file)
    {
        return file.Properties.Map(f => f.Length).Map(l => new ByteFlow(file.Contents, l));
    }

    public static ByteFlow ToByteFlow(this string str, Encoding encoding)
    {
        return new ByteFlow(encoding.GetBytes(str).ToObservable(), str.Length);
    }
}