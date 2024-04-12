using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage;

public static class AppImage
{
    public static Task<Result> FromAppDir(Stream stream, IDirectory appDir, IRuntime runtime)
    {
        return AppImageWriter.Write(stream, AppImageFactory.FromAppDir(appDir, runtime));
    }

    public static Task<Result> WriteFromBuildDirectory(Stream stream, IDirectory inputDir, SingleDirMetadata metadata)
    {
        return AppImageFactory.FromBuildDir(inputDir, metadata, architecture => new UriRuntime(architecture))
            .Bind(img => AppImageWriter.Write(stream, img));
    }

    public static Task<Result> WriteFromAppDir(Stream stream, IDirectory inputDir, Architecture architecture)
    {
        return AppImageWriter.Write(stream, AppImageFactory.FromAppDir(inputDir, new UriRuntime(architecture)));
    }
}