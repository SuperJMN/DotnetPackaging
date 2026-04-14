using System.Xml;
using System.Xml.Linq;

namespace DotnetPackaging.Msix.Core.Manifest;

/// <summary>
/// Metadata for generating a Store-compliant AppxManifest.xml.
/// </summary>
public class AppManifestMetadata
{
    // Identity
    public string Name { get; set; } = "com.example.app";
    public string Publisher { get; set; } = "CN=Publisher";
    public string Version { get; set; } = "1.0.0.0";
    public string ProcessorArchitecture { get; set; } = "x64";

    // Properties
    public string DisplayName { get; set; } = "App Name";
    public string PublisherDisplayName { get; set; } = "Publisher Name";
    public string Logo { get; set; } = @"Assets\StoreLogo.png";

    // Phone Identity (required by Partner Center)
    public string PhoneIdentity { get; set; } = Guid.NewGuid().ToString("D");

    // Application
    public string AppId { get; set; } = "App";
    public string Executable { get; set; } = "MyApp.exe";
    public string AppDisplayName { get; set; } = "Application Display Name";
    public string AppDescription { get; set; } = "Application Description";
    public string Square150x150Logo { get; set; } = @"Assets\Square150x150Logo.png";
    public string Square44x44Logo { get; set; } = @"Assets\Square44x44Logo.png";
    public string Wide310x150Logo { get; set; } = @"Assets\Wide310x150Logo.png";
    public string Square310x310Logo { get; set; } = @"Assets\Square310x310Logo.png";
    public string SplashScreen { get; set; } = @"Assets\SplashScreen.png";
    public string BackgroundColor { get; set; } = "transparent";
    public string ShortName { get; set; } = "App";

    // Dependencies
    public string MinVersion { get; set; } = "10.0.17763.0";
    public string MaxVersionTested { get; set; } = "10.0.22621.0";

    // Capabilities
    public bool InternetClient { get; set; } = true;
    public bool RunFullTrust { get; set; } = true;

    // Languages
    public IList<string> Languages { get; set; } = new List<string> { "x-generate" };
}

internal class AppManifestGenerator
{
    public static string GenerateAppManifest(AppManifestMetadata metadata)
    {
        using var memoryStream = new MemoryStream();
        WriteToStream(metadata, memoryStream);
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        return reader.ReadToEnd();
    }

    public static void WriteToStream(AppManifestMetadata metadata, Stream stream)
    {
        XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace mp = "http://schemas.microsoft.com/appx/2014/phone/manifest";
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
        XNamespace rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "Package",
                new XAttribute(XNamespace.Xmlns + "mp", mp),
                new XAttribute(XNamespace.Xmlns + "uap", uap),
                new XAttribute(XNamespace.Xmlns + "rescap", rescap),
                new XAttribute("IgnorableNamespaces", "uap rescap mp"),

                new XElement(ns + "Identity",
                    new XAttribute("Name", metadata.Name),
                    new XAttribute("Publisher", metadata.Publisher),
                    new XAttribute("Version", metadata.Version),
                    new XAttribute("ProcessorArchitecture", metadata.ProcessorArchitecture)),

                new XElement(mp + "PhoneIdentity",
                    new XAttribute("PhoneProductId", metadata.PhoneIdentity),
                    new XAttribute("PhonePublisherId", "00000000-0000-0000-0000-000000000000")),

                new XElement(ns + "Properties",
                    new XElement(ns + "DisplayName", metadata.DisplayName),
                    new XElement(ns + "PublisherDisplayName", metadata.PublisherDisplayName),
                    new XElement(ns + "Logo", metadata.Logo)),

                new XElement(ns + "Dependencies",
                    new XElement(ns + "TargetDeviceFamily",
                        new XAttribute("Name", "Windows.Desktop"),
                        new XAttribute("MinVersion", metadata.MinVersion),
                        new XAttribute("MaxVersionTested", metadata.MaxVersionTested))),

                new XElement(ns + "Resources",
                    metadata.Languages.Select(lang =>
                        new XElement(ns + "Resource", new XAttribute("Language", lang)))),

                new XElement(ns + "Applications",
                    new XElement(ns + "Application",
                        new XAttribute("Id", metadata.AppId),
                        new XAttribute("Executable", metadata.Executable),
                        new XAttribute("EntryPoint", "Windows.FullTrustApplication"),
                        new XElement(uap + "VisualElements",
                            new XAttribute("DisplayName", metadata.AppDisplayName),
                            new XAttribute("Description", metadata.AppDescription),
                            new XAttribute("BackgroundColor", metadata.BackgroundColor),
                            new XAttribute("Square150x150Logo", metadata.Square150x150Logo),
                            new XAttribute("Square44x44Logo", metadata.Square44x44Logo),
                            new XElement(uap + "DefaultTile",
                                new XAttribute("Wide310x150Logo", metadata.Wide310x150Logo),
                                new XAttribute("Square310x310Logo", metadata.Square310x310Logo),
                                new XAttribute("ShortName", metadata.ShortName)),
                            new XElement(uap + "SplashScreen",
                                new XAttribute("Image", metadata.SplashScreen))))),

                new XElement(ns + "Capabilities",
                    metadata.InternetClient ? new XElement(ns + "Capability", new XAttribute("Name", "internetClient")) : null,
                    metadata.RunFullTrust ? new XElement(rescap + "Capability", new XAttribute("Name", "runFullTrust")) : null)
            )
        );

        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = false,
            Encoding = System.Text.Encoding.UTF8
        };

        using var xmlWriter = XmlWriter.Create(stream, settings);
        doc.Save(xmlWriter);
    }
}
