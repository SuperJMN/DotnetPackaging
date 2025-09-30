using System.Security;
using System.Text;

namespace DotnetPackaging.Msix.Core.ContentTypes;

/// <summary>
/// Serializa el modelo de tipos de contenido a un XML exactamente como lo hace makeappx.
/// </summary>
public static class ContentTypesSerializer
{
    private const string Namespace = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static string Serialize(ContentTypesModel model)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        builder.Append($"<Types xmlns=\"{Namespace}\">");

        foreach (var defaultType in model.Defaults)
        {
            builder.Append("<Default Extension=\"");
            builder.Append(Escape(defaultType.Extension));
            builder.Append("\" ContentType=\"");
            builder.Append(Escape(defaultType.ContentType));
            builder.Append("\" />");
        }

        foreach (var overrideType in model.Overrides)
        {
            builder.Append("<Override PartName=\"");
            builder.Append(Escape(overrideType.PartName));
            builder.Append("\" ContentType=\"");
            builder.Append(Escape(overrideType.ContentType));
            builder.Append("\" />");
        }

        builder.Append("</Types>");
        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return SecurityElement.Escape(value) ?? value;
    }
}
