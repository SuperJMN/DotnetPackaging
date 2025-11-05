using CSharpFunctionalExtensions;

namespace DotnetPackaging.Publish;

public interface IPublisher
{
    Task<Result<PublishResult>> Publish(ProjectPublishRequest request);
}