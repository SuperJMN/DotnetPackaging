﻿using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Tests;

public class FromContainerOptions
{
    private readonly FakeRuntimeFactory runtimeFactory;
    private readonly ISlimDirectory container;

    public FromContainerOptions(FakeRuntimeFactory runtimeFactory, ISlimDirectory container)
    {
        this.runtimeFactory = runtimeFactory;
        this.container = container;
    }

    public FromContainer Configure(Action<ContainerOptionsSetup> setup)
    {
        var options = new ContainerOptionsSetup();
        setup(options);
        return new FromContainer(container, runtimeFactory, options);
    }
}