using System.Xml.Linq;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public class AppStreamXmlGenerator
{
    public static XElement GenerateXml(Metadata options)
    {
        var component = new XElement("component",
            new XAttribute("type", "desktop-application"),
            new XElement("id", options.AppId.GetValueOrDefault(options.AppName).ToLower()),
            new XElement("metadata_license", "CC0-1.0"),
            new XElement("project_license", "MIT"),
            new XElement("name", options.AppName),
            new XElement("summary", options.Summary),
            new XElement("description", new XElement("p", options.Comment)),
            new XElement("launchable", new XAttribute("type", "desktop-id"), $"{options.AppName}.desktop"),
            new XElement("url", new XAttribute("type", "homepage"), options.HomePage),
            GenerateScreenshots(options),
            new XElement("provides", new XElement("id", $"{options.AppName}.desktop"))
        );

        return component;
    }
    
    private static XElement GenerateScreenshots(Metadata options)
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

    private static XElement GenerateDescription(Metadata options)
    {
        var p = new XElement("p", $"{options.AppName} is a tool for file synchronization using AvaloniaUI.");
        var ul = new XElement("ul");
        options.Keywords.Execute(keywords =>
        {
            foreach (var keyword in keywords)
            {
                ul.Add(new XElement("li", keyword));
            }
        });

        return new XElement("description", p, ul);
    }
}