namespace DotnetPackaging;

public record PackageMetadata
{
    public PackageMetadata(string name, Architecture architecture, string package, string version)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name), "El nombre no puede ser nulo o vacío");
        }
        if (string.IsNullOrEmpty(package))
        {
            throw new ArgumentNullException(nameof(package), "El paquete no puede ser nulo o vacío");
        }
        if (string.IsNullOrEmpty(version))
        {
            throw new ArgumentNullException(nameof(version), "La versión no puede ser nula o vacía");
        }
        
        Architecture = architecture ?? throw new ArgumentNullException(nameof(architecture));
        Name = name;
        Package = package ?? throw new ArgumentNullException(nameof(package));
        Version = version ?? throw new ArgumentNullException(nameof(version));
    }

    /// <summary>
    /// Application Name, like "Power Statistics" or "Avalonia Syncer"
    /// </summary>
    public required string Name { get; init; }
    public required Architecture Architecture { get; init; }
    /// <summary>
    /// Package name like "PowerStatistics", usually commandline friendly. Will be used for icon names, executable names...
    /// </summary>
    public required string Package { get; init; }
    
    public required string Version { get; init; }
    public Maybe<string> StartupWmClass { get; init; } = Maybe<string>.None;
    public Maybe<IEnumerable<string>> Keywords { get; init; } = Maybe<IEnumerable<string>>.None;
    public Maybe<string> Comment { get; init; } = Maybe<string>.None;
    public Maybe<Categories> Categories { get; init; } = Maybe<Categories>.None;
    public Maybe<IIcon> Icon { get; init; } = Maybe<IIcon>.None;
    public Maybe<IEnumerable<Uri>> ScreenshotUrls { get; init; } = Maybe<IEnumerable<Uri>>.None;
    public Maybe<string> Summary { get; init; } = Maybe<string>.None;
    public Maybe<string> License { get; init; } = Maybe<string>.None;
    
    /// <summary>
    /// Identifier to univocally represent your application, like "org.gnome.gnome-power-statistics". Like a package full name.
    /// </summary>
    public Maybe<string> Id { get; init; } = Maybe<string>.None;
    public Maybe<string> Section { get; init; } = Maybe<string>.None;
    public Maybe<string> Priority { get; init; } = Maybe<string>.None;
    public Maybe<string> Maintainer { get; init; } = Maybe<string>.None;
    public Maybe<string> Description { get; init; } = Maybe<string>.None;
    public Maybe<Uri> Homepage { get; init; } = Maybe<Uri>.None;
    public Maybe<string> Recommends { get; init; } = Maybe<string>.None;
    public Maybe<string> VcsGit { get; init; } = Maybe<string>.None;
    public Maybe<string> VcsBrowser { get; init; } = Maybe<string>.None;
    public Maybe<long> InstalledSize { get; init; } = Maybe<long>.None;
    public required DateTimeOffset ModificationTime { get; init; }
}