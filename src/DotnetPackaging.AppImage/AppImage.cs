﻿using System;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using NyaFs.Processor.Scripting.Commands;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage;

public static class AppImage
{
    public static async Task<Result> Build(string contentFolderPath, string executableName, string outputPath, Architecture architecture)
    {
        var fs = new FileSystem();
        var contentDir = fs.DirectoryInfo.New(contentFolderPath);
        var outputFile = fs.FileInfo.New(outputPath);
        var bc = new DirectoryBlobContainer(contentDir.Name, contentDir);
        var executablePath = contentDir.Name + "/" + executableName;
        var build = new AppImageBuilder()
            .Build(bc, new UriRuntime(architecture), new DefaultScriptAppRun(executablePath));
        var writeResult = await build.Bind(async image =>
        {
            await using var fileSystemStream = outputFile.OpenWrite();
            return await AppImageWriter.Write(fileSystemStream, image);
        });
        return writeResult;
    }
}