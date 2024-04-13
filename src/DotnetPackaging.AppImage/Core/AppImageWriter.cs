using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public class AppImageWriter
{
    public static Task<Result> Write(Stream stream, AppImageBase appImage)
    {
        return appImage.Runtime.WriteTo(stream)
            .Bind(() => WritePayload(stream, appImage));
    }

    private static Task<Result> WritePayload(Stream stream, AppImageBase appImage)
    {
        return appImage.PayloadEntries()
            .Map(tuples => tuples.ToUnixFileList())
            .Bind(entries => SquashFS.Write(stream, entries));
    }
}