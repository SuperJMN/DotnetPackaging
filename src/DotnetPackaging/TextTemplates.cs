using CSharpFunctionalExtensions;
using Zafiro.Mixins;

namespace DotnetPackaging;

public static class TextTemplates
{
    public static string DesktopFileContents(string executablePath, PackageMetadata metadata)
    {
        List<IEnumerable<string>> items =
        [
            Item(Maybe.From("[Desktop Entry]")),
            Item(Maybe.From("Type=Application")),
            Item(metadata.StartupWmClass.Map(n => $"Name={n}")),
            Item(Maybe.From($"GenericName={metadata.AppName}")),
            Item(metadata.Comment.Map(n => $"Comment={n}")),
            Item(metadata.Icon.Map(_ => $"Icon={metadata.Package}")),
            Item("Terminal=False"),
            Item($"Exec=\"{executablePath}\""),
            Item(metadata.Categories.Map(x => $"Categories={x}")),
            Item(metadata.Keywords.Map(keywords => $"Keywords={string.Join((string?) ";", (IEnumerable<string?>) keywords)}")),
            Item(metadata.Version),
        ];

        return string.Join("\n", items.Flatten());
    }

    private static IEnumerable<string> Item(Maybe<string> from)
    {
        return from.ToList();
    }

    public static string RunScript(string executablePath)
    {
        return $"#!/usr/bin/env sh\n\"{executablePath}\" \"$@\"";
    }
}