namespace DotnetPackaging;

internal static class DesktopHostNames
{
    private static readonly string[] Suffixes =
    [
        ".Desktop",
        "-desktop",
        "_desktop",
        " desktop"
    ];

    public static string StripSuffix(string value)
    {
        return TryStripSuffix(value).GetValueOrDefault(value);
    }

    public static Maybe<string> TryStripSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Maybe<string>.None;
        }

        foreach (var suffix in Suffixes)
        {
            if (!value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) || value.Length <= suffix.Length)
            {
                continue;
            }

            return Maybe.From(value[..^suffix.Length]);
        }

        return Maybe<string>.None;
    }
}
