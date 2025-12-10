using CSharpFunctionalExtensions;

using Result = CSharpFunctionalExtensions.Result;
using IDisposableContainer = Zafiro.DivineBytes.IDisposableContainer;

namespace DotnetPackaging.Publish;

public interface IPublisher
{
    Task<Result<IDisposableContainer>> Publish(ProjectPublishRequest request);
}