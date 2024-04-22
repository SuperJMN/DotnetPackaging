using System.Text;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;

namespace DotnetPackaging.Deb.Archives;

public static class ArWriter
{
    public static Task<Result> Write(ArFile arFile, Stream stream)
    {
        return WriteHeader(stream)
            .Bind(() => WriteEntries(arFile, stream));
    }

    private static Result WriteHeader(Stream stream) => Result.Try(() => stream.WriteAsync("<!arch>\n".GetAsciiBytes()));

    private static Task<Result> WriteEntries(ArFile arFile, Stream stream)
    {
        return arFile.Entries
            .Select(entry => WriteEntry(entry, stream))
            .CombineInOrder();
    }

    private static Task<Result> WriteEntry(Entry entry, Stream output)
    {
        return Result
            .Try(() => output.WriteAsync(entry.File.Name.PadRight(16).GetAsciiBytes()))
            .Bind(_ => entry.File.Open()
                .Using(stream => WriteProperties(entry.Properties, stream.Length, output)
                    .Bind(() => Result.Try(() => stream.CopyToAsync(output)))));
    }

    private static  Task<Result> WriteProperties(Properties properties, long fileSize, Stream output)
    {
        return Result.Try(async () =>
        {
            await WritePaddedString(properties.LastModification.ToUnixTimeSeconds().ToString(), 12, output);
            await WritePaddedString(properties.OwnerId.GetValueOrDefault().ToString(), 6, output);
            await WritePaddedString(properties.GroupId.GetValueOrDefault().ToString(), 6, output);
            var fileModeOctal = Convert.ToString((uint) properties.FileMode, 8);
            await WritePaddedString("100" + fileModeOctal, 8, output);
            await WritePaddedString(fileSize.ToString(), 10, output);
        });
    }
    
    private static  async Task WritePaddedString(string str, int length, Stream output)
    {
        str = str.PadRight(length);
        await output.WriteAsync(Encoding.ASCII.GetBytes(str));
    }
}