using System.Globalization;
using System.Text.RegularExpressions;

namespace DotnetPackaging;

public static class ApplicationNameResolver
{
    public static string FromDirectory(Maybe<string> explicitName, string executableName)
    {
        if (explicitName.HasValue)
        {
            return explicitName.Value;
        }

        return HumanizeExecutableName(executableName);
    }

    public static string FromProject(Maybe<string> explicitName, Maybe<ProjectMetadata> projectMetadata, string executableName)
    {
        if (explicitName.HasValue)
        {
            return explicitName.Value;
        }

        var product = projectMetadata.Bind(x => x.Product);
        if (product.HasValue)
        {
            return product.Value;
        }

        var assemblyName = projectMetadata.Bind(x => x.AssemblyName);
        if (assemblyName.HasValue)
        {
            return HumanizeExecutableName(assemblyName.Value);
        }

        return HumanizeExecutableName(executableName);
    }

    private static string HumanizeExecutableName(string value)
    {
        var stripped = StripCommonSuffixes(RemoveExtension(value));
        var cleaned = Regex.Replace(stripped, "[._-]+", " ");
        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "Application";
        }

        var lower = cleaned.ToLowerInvariant();
        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        return textInfo.ToTitleCase(lower);
    }

    private static string StripCommonSuffixes(string name)
    {
        var result = name;
        var patterns = new[] { "-publish", "_publish", " publish", "-appdir", "_appdir", " appdir" };
        foreach (var p in patterns)
        {
            if (result.EndsWith(p, StringComparison.OrdinalIgnoreCase))
            {
                result = result[..^p.Length];
                break;
            }
        }

        return result;
    }

    private static string RemoveExtension(string value)
    {
        var extension = Path.GetExtension(value);
        return string.IsNullOrEmpty(extension) ? value : value[..^extension.Length];
    }
}
