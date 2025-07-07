// 1. AppStream actualizado

namespace DotnetPackaging.AppImage.Metadata;

public class AppStream
{
    // Required fields
    public string Id { get; set; }              // com.company.app (AppId)
    public string Name { get; set; }            // "My App Name" (AppName)
    public string Summary { get; set; }
    public string MetadataLicense { get; set; } = "CC0-1.0";

    // Optional fields
    public Maybe<string> ProjectLicense { get; set; }
    public Maybe<string> Description { get; set; }
    public Maybe<string> Homepage { get; set; }
    public Maybe<string> Icon { get; set; }
    public Maybe<IEnumerable<string>> Screenshots { get; set; }
    public Maybe<string> DesktopId { get; set; }  // packagename.desktop
}