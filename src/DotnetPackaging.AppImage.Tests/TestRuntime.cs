﻿using DotnetPackaging.AppImage.Core;
using Zafiro.Mixins;

namespace DotnetPackaging.AppImage.Tests;

public class TestRuntime : IRuntime
{
    public Func<Task<Result<Stream>>> StreamFactory => () => Task.FromResult(Result.Success("Stub runtime!".PadRight(1024).ToStream()));
}