using System.Collections.Immutable;

namespace MsixPackaging.Core.ContentTypes;

/// <summary>
/// Generador de Content Types que, a partir de la colección de nombres de partes,
/// construye un modelo inmutable con las entradas Default y Override.
/// </summary>
public static class ContentTypesGenerator
{
    // Diccionario de mappings por defecto, ajustado para emular makeappx:
    private static readonly Dictionary<string, string> DefaultMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "pdb", "application/octet-stream" },
        { "exe", "application/x-msdownload" },
        { "png", "image/png" },
        { "xml", "application/vnd.ms-appx.manifest+xml" }
        // Puedes agregar más según sea necesario.
    };

    // Diccionario de overrides predefinidos para partes conocidas.
    private static readonly Dictionary<string, string> OverrideMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Si se incluye el manifiesto, se puede generar como override o default, según la estrategia.
        // En el ejemplo de makeappx se trata a AppxManifest.xml como un archivo con default (al final aparece en el block map),
        // pero para [Content_Types].xml se suele definir override para el block map.
        { "/AppxBlockMap.xml", "application/vnd.ms-appx.blockmap+xml" }
        // Puedes agregar otros overrides si es necesario.
    };

    /// <summary>
    /// Genera un modelo inmutable de ContentTypesModel a partir de una colección de nombres de partes del paquete.
    /// Se agregan entradas Default basadas en la extensión y se añaden overrides para nombres conocidos.
    /// </summary>
    /// <param name="partNames">Colección de nombres de partes (por ejemplo, "folder/file.ext").</param>
    /// <returns>ContentTypesModel inmutable.</returns>
    public static ContentTypesModel Create(IEnumerable<string> partNames)
    {
        if (partNames == null)
            throw new ArgumentNullException(nameof(partNames));

        var defaultsBuilder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
        var overridesBuilder = ImmutableList.CreateBuilder<OverrideContentType>();

        foreach (var part in partNames)
        {
            // Normalizamos el nombre: aseguramos que empiece con "/" y reemplazamos '\' por '/'.
            string normalizedPart = part.StartsWith("/") ? part : "/" + part.Replace('\\', '/');

            // Si el nombre coincide con algún override predefinido, lo agregamos.
            if (OverrideMappings.TryGetValue(normalizedPart, out var overrideContentType))
            {
                overridesBuilder.Add(new OverrideContentType(normalizedPart, overrideContentType));
            }
            else
            {
                // Extraemos la extensión del archivo.
                string extension = GetExtension(part);
                if (!string.IsNullOrEmpty(extension))
                {
                    if (!defaultsBuilder.ContainsKey(extension))
                    {
                        if (!DefaultMappings.TryGetValue(extension, out var contentType))
                            contentType = "application/octet-stream";
                        defaultsBuilder.Add(extension, contentType);
                    }
                }
                else
                {
                    // Si no hay extensión, tratamos la parte como override.
                    overridesBuilder.Add(new OverrideContentType(normalizedPart, "application/octet-stream"));
                }
            }
        }

        var defaultsList = defaultsBuilder.Select(kvp => new DefaultContentType(kvp.Key, kvp.Value))
            .ToImmutableList();
        var overridesList = overridesBuilder.ToImmutableList();

        return new ContentTypesModel(defaultsList, overridesList);
    }

    private static string GetExtension(string partName)
    {
        // Extrae la extensión utilizando Path.GetExtension y remueve el punto.
        string ext = Path.GetExtension(partName);
        if (!string.IsNullOrEmpty(ext))
            return ext.TrimStart('.').ToLowerInvariant();
        return string.Empty;
    }
}