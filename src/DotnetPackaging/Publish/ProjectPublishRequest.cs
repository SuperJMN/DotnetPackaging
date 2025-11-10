using CSharpFunctionalExtensions;

namespace DotnetPackaging.Publish;

public sealed class ProjectPublishRequest
{
    public ProjectPublishRequest(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath)) throw new ArgumentException("Value cannot be null or empty", nameof(projectPath));
        ProjectPath = projectPath;
    }

    public string ProjectPath { get; }
    public Maybe<string> Rid { get; init; } = Maybe<string>.None;
    public bool SelfContained { get; init; } = true;
    public string Configuration { get; init; } = "Release";
    public bool SingleFile { get; init; }
    public bool Trimmed { get; init; }
    public IReadOnlyDictionary<string, string>? MsBuildProperties { get; init; }
}
