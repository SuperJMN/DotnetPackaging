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

        var assemblyName = projectMetadata.Bind(x => x.AssemblyName);
        var product = projectMetadata.Bind(x => x.Product);
        if (product.HasValue && !IsImplicitSdkDisplayName(product.Value, assemblyName))
        {
            return product.Value;
        }

        var assemblyTitle = projectMetadata.Bind(x => x.AssemblyTitle);
        if (assemblyTitle.HasValue && !IsImplicitSdkDisplayName(assemblyTitle.Value, assemblyName))
        {
            return assemblyTitle.Value;
        }

        if (assemblyName.HasValue)
        {
            return HumanizeExecutableName(assemblyName.Value);
        }

        return HumanizeExecutableName(executableName);
    }

    private static string HumanizeExecutableName(string value)
    {
        var stripped = StripCommonSuffixes(RemoveExecutableExtension(value));
        var cleaned = Regex.Replace(stripped, "[._-]+", " ");
        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "Application";
        }

        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        return string.Join(" ", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => HumanizePart(part, textInfo)));
    }

    private static string HumanizePart(string part, TextInfo textInfo)
    {
        if (part.Any(char.IsUpper))
        {
            return part;
        }

        return textInfo.ToTitleCase(part.ToLowerInvariant());
    }

    private static string StripCommonSuffixes(string name)
    {
        var result = DesktopHostNames.StripSuffix(name);
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

    private static string RemoveExecutableExtension(string value)
    {
        var extension = Path.GetExtension(value);
        return IsExecutableExtension(extension) ? value[..^extension.Length] : value;
    }

    private static bool IsExecutableExtension(string extension)
    {
        return string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
               || string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImplicitSdkDisplayName(string value, Maybe<string> assemblyName)
    {
        return assemblyName.HasValue
               && DesktopHostNames.TryStripSuffix(assemblyName.Value).HasValue
               && string.Equals(value, assemblyName.Value, StringComparison.Ordinal);
    }
}
