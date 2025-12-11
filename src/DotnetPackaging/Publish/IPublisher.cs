using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Publish;

public interface IPublisher
{
    Task<Result<IDisposableContainer>> Publish(ProjectPublishRequest request);
}
