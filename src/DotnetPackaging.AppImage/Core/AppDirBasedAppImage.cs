﻿using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public class AppDirBasedAppImage : AppImageBase
{
    private readonly IBlobContainer container;

    public AppDirBasedAppImage(IRuntime runtime, IBlobContainer container) : base(runtime)
    {
        this.container = container;
    }

    public override Task<Result<IEnumerable<(ZafiroPath Path, IBlob Blob)>>> PayloadEntries()
    {
        return container.GetBlobsInTree(ZafiroPath.Empty);
    }
}