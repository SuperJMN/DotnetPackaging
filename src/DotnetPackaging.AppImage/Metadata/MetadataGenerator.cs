using System.Xml.Linq;
using Zafiro.Mixins;

namespace DotnetPackaging.AppImage.Metadata;

public class MetadataGenerator
{
    public static string DesktopFileContents(DesktopFile metadata)
    {
        var desktopFileContents = $"""
                                   [Desktop Entry]
                                   Type=Application
                                   Name={metadata.Name}
                                   Exec="{metadata.Exec}"
                                   Terminal={metadata.IsTerminal.ToString().ToLower()}
                                   {metadata.StartupWmClass.Map(s => $"StartupWmClass={s}").GetValueOrDefault("")}
                                   {metadata.Icon.Map(s => $"Icon={s}").GetValueOrDefault("")}
                                   {metadata.Comment.Map(s => $"Comment={s}").GetValueOrDefault("")}
                                   {metadata.Categories.Map(s => $"Categories={s.JoinWith(";")};").GetValueOrDefault("")}
                                   {metadata.Keywords.Map(s => $"Keywords={s.JoinWith(";")};").GetValueOrDefault("")}
                                   {metadata.Version.Map(s => $"X-AppImage-Version={s}").GetValueOrDefault("")}
                                   """;

        return desktopFileContents;
    }

    public static string AppStreamXml(AppStream packageMetadata)
    {
        var elements = new List<object>
        {
            new XAttribute("type", "desktop-application"),
            new XElement("id", packageMetadata.Id),
            new XElement("metadata_license", packageMetadata.MetadataLicense),
            new XElement("name", packageMetadata.Name),
            new XElement("summary", packageMetadata.Summary)
        };

        // Add optional elements only if they have values
        packageMetadata.ProjectLicense.Match(
            license => elements.Add(new XElement("project_license", license)),
            () => { });

        packageMetadata.Description.Match(
            desc => elements.Add(new XElement("description", new XElement("p", desc))),
            () => { });

        // Launchable is required for desktop-application, use DesktopId or fallback
        elements.Add(new XElement("launchable",
            new XAttribute("type", "desktop-id"),
            packageMetadata.DesktopId.GetValueOrDefault($"{packageMetadata.Name}.desktop")));

        packageMetadata.Homepage.Match(
            url => elements.Add(new XElement("url", new XAttribute("type", "homepage"), url)),
            () => { });

        packageMetadata.Screenshots.Match(
            screenshots => elements.Add(GenerateScreenshots(screenshots)),
            () => { });

        // Always add provides section
        elements.Add(new XElement("provides",
            new XElement("id", packageMetadata.DesktopId.GetValueOrDefault($"{packageMetadata.Name}.desktop"))));

        var xElement = new XElement("component", elements);
        return xElement.ToString();
    }

    private static XElement GenerateScreenshots(IEnumerable<string> screenshots)
    {
        return new XElement("screenshots",
            screenshots.Select(screenshot =>
                new XElement("screenshot",
                    new XElement("image", screenshot))));
    }
}