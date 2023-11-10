using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.IO;

namespace DotnetPackaging.Tar;

public static class ByteSeq 
{
    public static Task<Result<FiniteByteSequence>> ToByteStream(this IZafiroFile file)
    {
        return file.GetContents().CombineAndMap(file.Size(), (stream, l) =>
        {
            return new FiniteByteSequence(Observable.Using(() => stream, s => s.ToObservable()), l);
        });
    }
}