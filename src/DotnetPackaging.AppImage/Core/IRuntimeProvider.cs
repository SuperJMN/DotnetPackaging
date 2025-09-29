using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public interface IRuntimeProvider
{
    Task<Result<IRuntime>> Create(Architecture architecture);
}

internal sealed class DefaultRuntimeProvider : IRuntimeProvider
{
    public Task<Result<IRuntime>> Create(Architecture architecture) => RuntimeFactory.Create(architecture);
}
