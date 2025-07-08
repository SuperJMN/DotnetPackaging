namespace DotnetPackaging.Deployment.Core;

public class Context(IDotnet dotnet, ICommand command, Maybe<ILogger> logger, IHttpClientFactory httpClientFactory)
{
    public Maybe<ILogger> Logger { get; } = logger;
    public IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
    public IDotnet Dotnet { get; } = dotnet;
    public ICommand Command { get; } = command;
}