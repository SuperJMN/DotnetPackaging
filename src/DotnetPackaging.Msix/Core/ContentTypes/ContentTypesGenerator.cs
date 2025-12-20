using System.Collections.Generic;
using System.Collections.Immutable;
using Path = System.IO.Path;

namespace DotnetPackaging.Msix.Core.ContentTypes;

/// <summary>
/// Genera el modelo de tipos de contenido ([Content_Types].xml) emulando el comportamiento de makeappx.
/// </summary>
internal static class ContentTypesGenerator
{
    private static readonly Dictionary<string, string> DefaultMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "pdb", "application/octet-stream" },
        { "exe", "application/x-msdownload" },
        { "png", "image/png" },
        { "xml", "application/vnd.ms-appx.manifest+xml" }
    };

    private static readonly Dictionary<string, string> OverrideMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "/AppxBlockMap.xml", "application/vnd.ms-appx.blockmap+xml" }
    };

    public static ContentTypesModel Create(IEnumerable<string> partNames)
    {
        if (partNames == null)
            throw new ArgumentNullException(nameof(partNames));

        var defaults = new List<DefaultContentType>();
        var overrides = new List<OverrideContentType>();
        var seenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenOverrides = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in partNames)
        {
            string normalizedPart = NormalizePartName(part);

            if (OverrideMappings.TryGetValue(normalizedPart, out var overrideContentType))
            {
                if (seenOverrides.Add(normalizedPart))
                {
                    overrides.Add(new OverrideContentType(normalizedPart, overrideContentType));
                }

                continue;
            }

            string extension = GetExtension(part);
            if (!string.IsNullOrEmpty(extension))
            {
                if (seenExtensions.Add(extension))
                {
                    if (!DefaultMappings.TryGetValue(extension, out var contentType))
                    {
                        contentType = "application/octet-stream";
                    }

                    defaults.Add(new DefaultContentType(extension, contentType));
                }

                continue;
            }

            if (seenOverrides.Add(normalizedPart))
            {
                overrides.Add(new OverrideContentType(normalizedPart, "application/octet-stream"));
            }
        }

        return new ContentTypesModel(defaults.ToImmutableList(), overrides.ToImmutableList());
    }

    private static string NormalizePartName(string part)
    {
        return part.StartsWith("/") ? part : "/" + part.Replace('\\', '/');
    }

    private static string GetExtension(string partName)
    {
        string ext = Path.GetExtension(partName);
        return string.IsNullOrEmpty(ext) ? string.Empty : ext.TrimStart('.').ToLowerInvariant();
    }
}
