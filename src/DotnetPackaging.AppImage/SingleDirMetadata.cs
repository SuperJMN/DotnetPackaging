namespace DotnetPackaging.AppImage;

public class SingleDirMetadata
{
    public required string AppName { get; init; }
    public required string StartupWmClass { get; set; }
    public required List<string> Keywords { get; init; }
    public required string Comment { get; init; }
    public required List<string> Categories { get; init; }
}