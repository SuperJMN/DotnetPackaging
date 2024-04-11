using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using System.Runtime.InteropServices;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage;

public static class AppImage
{
    public static Task<Result> FromAppDir(Stream stream, IDirectory appDir, IRuntime uriRuntime)
    {
        return AppImageWriter.Write(stream, AppImageFactory.FromAppDir(appDir, uriRuntime));
    }
    
    public static Task<Result> FromBuildDir(Stream stream, IDirectory buildDir, Func<Architecture, IRuntime> getRuntime)
    {
        return AppImageFactory.FromBuildDir(buildDir, Maybe<DesktopMetadata>.None, getRuntime: getRuntime).Bind(appImage => AppImageWriter.Write(stream, appImage));
    }
}