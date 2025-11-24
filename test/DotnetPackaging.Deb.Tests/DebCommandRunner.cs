using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Deb.Archives.Deb;
using System.IO.Abstractions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;

namespace DotnetPackaging.Deb.Tests;

internal static class DebCommandRunner
{
    public static Task<int> BuildDebAsync(string sourceDirectory, string outputFilePath)
    {
        return BuildWithApiAsync(sourceDirectory, outputFilePath);
    }

    private static async Task<int> BuildWithApiAsync(string sourceDirectory, string outputFilePath)
    {
        var fs = new FileSystem();
        var dirInfo = new DirectoryInfo(sourceDirectory);
        var container = new DirectoryContainer(new DirectoryInfoWrapper(fs, dirInfo)).AsRoot();

        var options = new Options
        {
            Name = Maybe.From("Sample App"),
            Version = Maybe.From("1.0.0"),
            Comment = Maybe.From("Sample package for tests"),
            ExecutableName = Maybe.From("sample-app")
        };

        var result = await DotnetPackaging.Deb.DebFile.From()
            .Container(container, "Sample App")
            .Configure(cfg => cfg.From(options))
            .Build();

        if (result.IsFailure)
        {
            return -1;
        }

        var data = DebMixin.ToData(result.Value);
        await using var stream = File.Open(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await ByteSource.FromByteObservable(data.Bytes).ToStream().CopyToAsync(stream);
        return 0;
    }
}
