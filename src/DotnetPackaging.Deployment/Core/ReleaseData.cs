namespace DotnetPackaging.Deployment.Core;

public class ReleaseData(string releaseName, string tag, string releaseBody, bool isDraft = false, bool isPrerelease = false)
{
    public string ReleaseName { get; } = releaseName;
    public string Tag { get; } = tag;
    public string ReleaseBody { get; } = releaseBody;
    public bool IsDraft { get; } = isDraft;
    public bool IsPrerelease { get; } = isPrerelease;
}