using System.Collections.Generic;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Publish;

public static class MsBuildVersionPropertiesFactory
{
    public static IReadOnlyDictionary<string, string>? Create(Maybe<string> version)
    {
        return version.Match(
            value => string.IsNullOrWhiteSpace(value)
                ? null
                : BuildProperties(value),
            () => null);
    }

    private static IReadOnlyDictionary<string, string> BuildProperties(string value)
    {
        var properties = new Dictionary<string, string>
        {
            ["Version"] = value,
            ["InformationalVersion"] = value
        };

        var normalizedAssemblyVersion = NormalizeAssemblyVersion(value);

        if (normalizedAssemblyVersion is not null)
        {
            properties["FileVersion"] = normalizedAssemblyVersion;
            properties["AssemblyVersion"] = normalizedAssemblyVersion;
        }

        return properties;
    }

    private static string? NormalizeAssemblyVersion(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.Length == 0)
        {
            return null;
        }

        var numericPrefixLength = 0;

        while (numericPrefixLength < trimmed.Length)
        {
            var character = trimmed[numericPrefixLength];

            if (char.IsDigit(character) || character == '.')
            {
                numericPrefixLength++;
                continue;
            }

            break;
        }

        if (numericPrefixLength == 0)
        {
            return null;
        }

        var numericPortion = trimmed[..numericPrefixLength].Trim('.');

        if (numericPortion.Length == 0)
        {
            return null;
        }

        var segments = numericPortion.Split('.');

        if (segments.Length == 0)
        {
            return null;
        }

        var normalizedSegments = new List<int>(capacity: 4);

        foreach (var segment in segments)
        {
            if (normalizedSegments.Count == 4)
            {
                break;
            }

            if (segment.Length == 0 || !int.TryParse(segment, out var numericSegment))
            {
                return null;
            }

            normalizedSegments.Add(numericSegment);
        }

        if (normalizedSegments.Count == 0)
        {
            return null;
        }

        while (normalizedSegments.Count < 4)
        {
            normalizedSegments.Add(0);
        }

        return string.Join('.', normalizedSegments);
    }
}
