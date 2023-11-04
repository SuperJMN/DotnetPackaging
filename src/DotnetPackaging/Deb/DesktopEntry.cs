namespace DotnetPackaging.Deb;

public class DesktopEntry
{
    public required IconResources Icons { get; init; }
    public required string Name { get; init; }
    public required string StartupWmClass { get; set; }
    public required IEnumerable<string> Keywords { get; init; }
}