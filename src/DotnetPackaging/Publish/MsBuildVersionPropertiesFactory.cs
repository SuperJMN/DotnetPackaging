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
                : new Dictionary<string, string>
                {
                    ["Version"] = value,
                    ["FileVersion"] = value,
                    ["AssemblyVersion"] = value,
                    ["InformationalVersion"] = value
                },
            () => null);
    }
}
