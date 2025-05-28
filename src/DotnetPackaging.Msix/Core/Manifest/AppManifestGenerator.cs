using System.Xml;
using System.Xml.Linq;

namespace DotnetPackaging.Msix.Core.Manifest;

/// <summary>
/// Clase para almacenar los metadatos esenciales del AppManifest.xml
/// </summary>
public class AppManifestMetadata
{
    // Identity
    public string Name { get; set; } = "com.example.app";
    public string Publisher { get; set; } = "CN=Publisher";
    public string Version { get; set; } = "1.0.0.0";

    // Properties
    public string DisplayName { get; set; } = "App Name";
    public string PublisherDisplayName { get; set; } = "Publisher Name";
    public string Logo { get; set; } = "Assets\\StoreLogo.png";

    // Application
    public string AppId { get; set; } = "App";
    public string Executable { get; set; } = "MyApp.exe";
    public string AppDisplayName { get; set; } = "Application Display Name";
    public string AppDescription { get; set; } = "Application Description";
    public string Square150x150Logo { get; set; } = "Assets\\Square150x150Logo.png";
    public string Square44x44Logo { get; set; } = "Assets\\Square44x44Logo.png";
    public string BackgroundColor { get; set; } = "transparent";

    // Capabilities
    public bool InternetClient { get; set; } = true;
    public bool RunFullTrust { get; set; } = true;
}

public class AppManifestGenerator
{
    /// <summary>
    /// Genera un string con el contenido XML del AppManifest a partir de los metadatos proporcionados
    /// </summary>
    /// <param name="metadata">Metadatos para el AppManifest</param>
    /// <returns>String con el contenido XML del AppManifest</returns>
    public static string GenerateAppManifest(AppManifestMetadata metadata)
    {
        using (var memoryStream = new MemoryStream())
        {
            WriteToStream(metadata, memoryStream);
            memoryStream.Position = 0;
            using (var reader = new StreamReader(memoryStream))
            {
                return reader.ReadToEnd();
            }
        }
    }

    /// <summary>
    /// Escribe el contenido XML del AppManifest en un stream
    /// </summary>
    /// <param name="metadata">Metadatos para el AppManifest</param>
    /// <param name="stream">Stream donde se escribirá el XML</param>
    public static void WriteToStream(AppManifestMetadata metadata, Stream stream)
    {
        XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
        XNamespace rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

        // Crear el documento XML
        XDocument doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "Package",
                new XAttribute(XNamespace.Xmlns + "uap", uap),
                new XAttribute(XNamespace.Xmlns + "rescap", rescap),
                new XAttribute("IgnorableNamespaces", "uap rescap"),

                // Identity
                new XElement(ns + "Identity",
                    new XAttribute("Name", metadata.Name),
                    new XAttribute("Publisher", metadata.Publisher),
                    new XAttribute("Version", metadata.Version)),

                // Properties
                new XElement(ns + "Properties",
                    new XElement(ns + "DisplayName", metadata.DisplayName),
                    new XElement(ns + "PublisherDisplayName", metadata.PublisherDisplayName),
                    new XElement(ns + "Logo", metadata.Logo)),

                // Dependencies
                new XElement(ns + "Dependencies",
                    new XElement(ns + "TargetDeviceFamily",
                        new XAttribute("Name", "Windows.Universal"),
                        new XAttribute("MinVersion", "10.0.0.0"),
                        new XAttribute("MaxVersionTested", "10.0.0.0")),
                    new XElement(ns + "TargetDeviceFamily",
                        new XAttribute("Name", "Windows.Desktop"),
                        new XAttribute("MinVersion", "10.0.14393.0"),
                        new XAttribute("MaxVersionTested", "10.0.14393.0"))),

                // Resources
                new XElement(ns + "Resources",
                    new XElement(ns + "Resource",
                        new XAttribute("Language", "en-US"))),

                // Applications
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
                            new XAttribute("Square44x44Logo", metadata.Square44x44Logo)))),

                // Capabilities
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
            
        using (var xmlWriter = XmlWriter.Create(stream, settings))
        {
            doc.Save(xmlWriter);
        }
    }
}