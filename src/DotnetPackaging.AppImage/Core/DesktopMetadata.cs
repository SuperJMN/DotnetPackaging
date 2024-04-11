namespace DotnetPackaging.AppImage.Core;

public class DesktopMetadata
{
    public required string Name { get; init; }
    public required string StartupWmClass { get; set; }
    public required IEnumerable<string> Keywords { get; init; }
    public required string Comment { get; init; }
    public required IEnumerable<string> Categories { get; init; }
    public required string ExecutablePath { get; init; }
}