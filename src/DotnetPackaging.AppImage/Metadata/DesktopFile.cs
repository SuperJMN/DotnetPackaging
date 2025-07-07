namespace DotnetPackaging.AppImage.Metadata;

public class DesktopFile
{
    public string Name { get; set; }             // "My App Name" (AppName)
    public string Exec { get; set; }
    public bool IsTerminal { get; set; }
    public Maybe<string> StartupWmClass { get; set; }
    public Maybe<string> Icon { get; set; }      // packagename (icon name)
    public Maybe<string> Comment { get; set; }
    public Maybe<IEnumerable<string>> Categories { get; set; }
    public Maybe<IEnumerable<string>> Keywords { get; set; }
    public Maybe<string> Version { get; set; }
}