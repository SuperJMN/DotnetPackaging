namespace DotnetPackaging.AppImage.Metadata;

public class AppImageMetadata
{
    public string AppId { get; init; }           // "com.mycompany.fileexplorer"
    public string AppName { get; init; }         // "My File Explorer"
    public string PackageName { get; init; }    // "fileexplorer"

    // Metadata fields
    public Maybe<string> Summary { get; init; } = Maybe<string>.None;
    public Maybe<string> Comment { get; init; } = Maybe<string>.None;
    public Maybe<string> Description { get; init; } = Maybe<string>.None;
    public Maybe<string> Version { get; init; } = Maybe<string>.None;
    public Maybe<string> Homepage { get; init; } = Maybe<string>.None;
    public Maybe<string> ProjectLicense { get; init; } = Maybe<string>.None;
    public Maybe<IEnumerable<string>> Categories { get; init; } = Maybe<IEnumerable<string>>.None;
    public Maybe<IEnumerable<string>> Keywords { get; init; } = Maybe<IEnumerable<string>>.None;
    public Maybe<IEnumerable<string>> Screenshots { get; init; } = Maybe<IEnumerable<string>>.None;
    public Maybe<string> StartupWmClass { get; init; } = Maybe<string>.None;
    public bool IsTerminal { get; init; } = false;

    public AppImageMetadata(string appId, string appName, string packageName)
    {
        AppId = appId;
        AppName = appName;
        PackageName = packageName;
    }

    // Convenience constructor
    public AppImageMetadata(string appName, string? packageName = null)
        : this(
            appId: (packageName ?? appName).ToLowerInvariant().Replace(" ", "").Replace("-", ""),
            appName: appName,
            packageName: packageName ?? appName.ToLowerInvariant().Replace(" ", ""))
    {
    }

    // Auxiliary methods to convert to DesktopFile and AppStream
    public DesktopFile ToDesktopFile(string execPath)
    {
        return new DesktopFile
        {
            Name = AppName,
            Exec = execPath,
            IsTerminal = IsTerminal,
            StartupWmClass = StartupWmClass,
            Icon = Maybe<string>.From(PackageName), // Use package name as icon
            Comment = Comment,
            Categories = Categories,
            Keywords = Keywords,
            Version = Version
        };
    }

    public AppStream ToAppStream()
    {
        return new AppStream
        {
            Id = AppId,
            Name = AppName,
            Summary = Summary.GetValueOrDefault(Comment.GetValueOrDefault(AppName)),
            ProjectLicense = ProjectLicense,
            Description = Description,
            Homepage = Homepage,
            Icon = Maybe<string>.From(PackageName), // Use package name as icon
            Screenshots = Screenshots,
            DesktopId = Maybe<string>.From($"{PackageName}.desktop")
        };
    }

    public string DesktopFileName => $"{PackageName}.desktop";
    public string AppDataFileName => $"{PackageName}.appdata.xml";
    public string AppImageFileName => $"{PackageName}.AppImage";
    public string IconName => PackageName;
}