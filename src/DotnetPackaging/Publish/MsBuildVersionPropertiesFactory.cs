using System;
using System.Collections.Generic;
using CSharpFunctionalExtensions;
using NuGet.Versioning;

namespace DotnetPackaging.Publish;

public static class MsBuildVersionPropertiesFactory
{
    private const int AssemblyVersionSegmentCount = 4;
    private const int MaxAssemblyVersionComponentValue = ushort.MaxValue;

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

        if (TryNormalizeFromNuGetVersion(trimmed, out var normalized))
        {
            return normalized;
        }

        return NormalizeFromNumericPrefix(trimmed);
    }

    private static bool TryNormalizeFromNuGetVersion(string value, out string? normalized)
    {
        if (!NuGetVersion.TryParse(value, out var parsed))
        {
            normalized = null;
            return false;
        }

        var components = new[]
        {
            parsed.Major,
            parsed.Minor,
            parsed.Patch,
            parsed.Revision >= 0 ? parsed.Revision : 0
        };

        normalized = NormalizeComponents(components);

        return normalized is not null;
    }

    private static string? NormalizeFromNumericPrefix(string value)
    {
        var numericPrefixLength = 0;

        while (numericPrefixLength < value.Length)
        {
            var character = value[numericPrefixLength];

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

        var numericPortion = value[..numericPrefixLength].Trim('.');

        if (numericPortion.Length == 0)
        {
            return null;
        }

        var segments = numericPortion.Split('.');

        if (segments.Length == 0)
        {
            return null;
        }

        var normalizedSegments = new List<int>(capacity: AssemblyVersionSegmentCount);

        foreach (var segment in segments)
        {
            if (normalizedSegments.Count == AssemblyVersionSegmentCount)
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

        return NormalizeComponents(normalizedSegments);
    }

    private static string? NormalizeComponents(IReadOnlyList<int> components)
    {
        if (components.Count == 0)
        {
            return null;
        }

        var normalizedSegments = new int[AssemblyVersionSegmentCount];
        var relevantSegments = Math.Min(components.Count, AssemblyVersionSegmentCount);

        for (var i = 0; i < relevantSegments; i++)
        {
            var component = components[i];

            if (!IsValidAssemblyComponent(component))
            {
                return null;
            }

            normalizedSegments[i] = component;
        }

        for (var i = relevantSegments; i < AssemblyVersionSegmentCount; i++)
        {
            normalizedSegments[i] = 0;
        }

        return string.Join('.', normalizedSegments);
    }

    private static bool IsValidAssemblyComponent(int value)
    {
        return value >= 0 && value <= MaxAssemblyVersionComponentValue;
    }
}
