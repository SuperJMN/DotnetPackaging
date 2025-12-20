namespace DotnetPackaging.Publish;

public sealed class ProjectPublishRequest
{
    public ProjectPublishRequest(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath)) throw new ArgumentException("Value cannot be null or empty", nameof(projectPath));
        ProjectPath = projectPath;
    }

    public string ProjectPath { get; }
    public Maybe<string> Rid { get; set; } = Maybe<string>.None;
    public bool SelfContained { get; set; } = true;
    public string Configuration { get; set; } = "Release";
    public bool SingleFile { get; set; }
    public bool Trimmed { get; set; }
    public IReadOnlyDictionary<string, string>? MsBuildProperties { get; set; }
}
