namespace DotnetPackaging.Deb.Archives.Deb;

public record Metadata
{
    public string Package { get; set; }
    public string Version { get; set; }
    public string Section { get; set; }
    public string Priority { get; set; }
    public string Architecture { get; set; }
    public string Maintainer { get; set; }
    public string Description { get; set; }
    public string Homepage { get; set; }
    public string License { get; set; }
    public string Recommends { get; set; }
    public string VcsGit { get; set; }
    public string VcsBrowser { get; set; }
    public string InstalledSize { get; set; }
    public DateTimeOffset ModificationTime { get; set; }
}