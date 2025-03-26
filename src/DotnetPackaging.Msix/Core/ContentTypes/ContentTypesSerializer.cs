using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DotnetPackaging.Msix.Core.ContentTypes;

/// <summary>
/// Serializador del modelo de Content Types a XML, generando un [Content_Types].xml conforme a la especificación.
/// </summary>
public static class ContentTypesSerializer
{
    /// <summary>
    /// Serializa el ContentTypesModel a un string XML.
    /// </summary>
    /// <param name="model">Modelo de content types.</param>
    /// <returns>XML en formato string con la declaración UTF-8.</returns>
    public static string Serialize(ContentTypesModel model)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
        var typesElement = new XElement(ns + "Types",
            model.Defaults.Select(d =>
                new XElement(ns + "Default",
                    new XAttribute("Extension", d.Extension),
                    new XAttribute("ContentType", d.ContentType)
                )
            ),
            model.Overrides.Select(o =>
                new XElement(ns + "Override",
                    new XAttribute("PartName", o.PartName),
                    new XAttribute("ContentType", o.ContentType)
                )
            )
        );

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "no"), typesElement);

        // Configuramos XmlWriterSettings para que use UTF8 y se incluya la cabecera
        var settings = new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            Indent = true,
            OmitXmlDeclaration = false
        };

        using (var ms = new MemoryStream())
        using (var writer = XmlWriter.Create(ms, settings))
        {
            doc.Save(writer);
            writer.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}