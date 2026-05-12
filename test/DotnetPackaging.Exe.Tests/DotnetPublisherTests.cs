using CSharpFunctionalExtensions;
using DotnetPackaging.Publish;
using FluentAssertions;
using Zafiro.Commands;

namespace DotnetPackaging.Exe.Tests;

public class DotnetPublisherTests
{
    [Fact]
    public async Task Publish_runs_dotnet_publish_serially_without_reused_build_processes()
    {
        var command = new RecordingCommand();
        var publisher = new DotnetPublisher(command, Maybe<Serilog.ILogger>.None);
        var request = new ProjectPublishRequest(@"C:\Project\App.csproj")
        {
            Rid = Maybe<string>.From("win-x64"),
            SelfContained = true,
            Configuration = "Release"
        };

        var result = await publisher.Publish(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dispose();

        var publish = command.Executions.Should()
            .ContainSingle(execution => execution.Arguments.StartsWith("publish ", StringComparison.Ordinal))
            .Subject;
        publish.Arguments.Should().Contain("-m:1");
        publish.Arguments.Should().Contain("/nr:false");
        publish.Arguments.Should().Contain("/p:UseSharedCompilation=false");
        publish.EnvironmentVariables.Should().Contain("MSBUILDDISABLENODEREUSE", "1");
    }

    private sealed class RecordingCommand : ICommand
    {
        public List<Execution> Executions { get; } = [];

        public Task<Result<string>> Execute(
            string fileName,
            string arguments,
            string workingDirectory = "",
            Dictionary<string, string>? environmentVariables = null)
        {
            Executions.Add(new Execution(fileName, arguments, environmentVariables ?? new Dictionary<string, string>()));
            return Task.FromResult(Result.Success("App"));
        }
    }

    private sealed record Execution(string FileName, string Arguments, Dictionary<string, string> EnvironmentVariables);
}
