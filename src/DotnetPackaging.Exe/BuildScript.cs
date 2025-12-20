using System.IO;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Metadata;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe;

internal static class BuildScript
{
    public static async Task<Result> Build(
        IByteSource stub,
        IContainer publishDirectory,
        InstallerMetadata metadata,
        Maybe<IByteSource> logo,
        FileInfo installerOutput,
        FileInfo uninstallerOutput)
    {
        var buildResult = await SimpleExePacker.Build(stub, publishDirectory, metadata, logo);
        if (buildResult.IsFailure)
        {
            return Result.Failure(buildResult.Error);
        }

        return await buildResult.Value.WriteTo(installerOutput, uninstallerOutput);
    }
}
