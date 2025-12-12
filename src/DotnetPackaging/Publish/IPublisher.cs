namespace DotnetPackaging.Publish;

public interface IPublisher
{
    Task<Result<IDisposableContainer>> Publish(ProjectPublishRequest request);
}
