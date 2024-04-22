using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Archives.Ar;
using Zafiro.CSharpFunctionalExtensions;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class TarWriter
{
    public static Task<Result> Write(TarFile arFile, Stream stream)
    {
        throw new NotImplementedException();
        //return WriteHeader(stream)
        //    .Bind(() => WriteEntries(arFile, stream));
    }

    //private static Result WriteHeader(Stream stream) => Result.Try(() => stream.WriteAsync("!<arch>\n".GetAsciiBytes()));

    //private static Task<Result> WriteEntries(TarFile arFile, Stream stream)
    //{
    //    return arFile.Entries
    //        .Select(entry => WriteEntry(entry, stream))
    //        .CombineInOrder();
    //}

    //private static Task<Result> WriteEntry(TarEntry entry, Stream output)
    //{
    //    return Result
    //        .Try(() => output.WriteAsync(entry.File.Name.PadRight(16).GetAsciiBytes()))
    //        .Bind(_ => entry.File.Open()
    //            .Using(stream =>
    //            {
    //                return WriteProperties(entry.TarProperties, stream.Length, output)
    //                    .Bind(() => Result.Try(() => output.WriteAsync("`\n".GetAsciiBytes())))
    //                    .Bind(_ => Result.Try(() => stream.CopyToAsync(output)));
    //            }));
    //}

    //private static Task<Result> WriteProperties(TarProperties tarProperties, long fileSize, Stream output)
    //{
    //    return Result.Try(async () =>
    //    {
    //        await WritePaddedString(tarProperties.LastModification.ToUnixTimeSeconds().ToString(), 12, output);
    //        await WritePaddedString(tarProperties.OwnerId.GetValueOrDefault().ToString(), 6, output);
    //        await WritePaddedString(tarProperties.GroupId.GetValueOrDefault().ToString(), 6, output);
    //        var fileModeOctal = Convert.ToString((uint)tarProperties.FileMode, 8);
    //        await WritePaddedString("100" + fileModeOctal, 8, output);
    //        await WritePaddedString(fileSize.ToString(), 10, output);
    //    });
    //}

    //private static async Task WritePaddedString(string str, int length, Stream output)
    //{
    //    str = str.PadRight(length);
    //    await output.WriteAsync(Encoding.ASCII.GetBytes(str));
    //}
}