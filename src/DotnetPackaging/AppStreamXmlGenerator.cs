using System.Xml.Linq;

namespace DotnetPackaging;

public static class AppStreamXmlGenerator
{
    public static XElement GenerateXml(PackageMetadata options)
    {
        var component = new XElement("component",
            new XAttribute("type", "desktop-application"),
            new XElement("id", options.Id.GetValueOrDefault(options.Name).ToLower()),
            new XElement("metadata_license", "CC0-1.0"),
            new XElement("project_license", "MIT"),
            new XElement("name", options.Name),
            new XElement("summary", options.Summary),
            new XElement("description", new XElement("p", options.Comment)),
            new XElement("launchable", new XAttribute("type", "desktop-id"), $"{options.Name}.desktop"),
            new XElement("url", new XAttribute("type", "homepage"), options.Homepage),
            GenerateScreenshots(options),
            new XElement("provides", new XElement("id", $"{options.Name}.desktop"))
        );

        return component;
    }
    
    private static XElement GenerateScreenshots(PackageMetadata options)
    {
        var screenshotsElement = new XElement("screenshots");
        
        options.ScreenshotUrls.Execute(urls =>
        {
            foreach (var url in urls)
            {
                screenshotsElement.Add(new XElement("screenshot", new XAttribute("type", "default"), new XElement("image", url)));
            }
        });
       
        return screenshotsElement;
    }
}