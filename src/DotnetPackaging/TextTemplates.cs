using System.Linq;
using Zafiro.Mixins;

namespace DotnetPackaging;

public static class TextTemplates
{
    public static string DesktopFileContents(string executablePath, PackageMetadata metadata)
    {
        var items = new[]
        {
            Maybe.From("[Desktop Entry]"),
            Maybe.From("Type=Application"),
            Maybe.From($"Name={metadata.Name}"),
            metadata.StartupWmClass.Map(n => $"StartupWMClass={n}"),
            metadata.Comment.Map(n => $"Comment={n}"),
            metadata.IconFiles.Any()
                ? Maybe.From($"Icon={metadata.Package.ToLowerInvariant()}")
                : Maybe<string>.None,
            Maybe.From($"Terminal={metadata.IsTerminal.ToString().ToLower()}"),
            Maybe.From($"Exec=\"{executablePath}\""),
            metadata.Categories.Map(x => $"Categories={x}"),
            metadata.Keywords.Map(keywords => $"Keywords={keywords.JoinWith(";")}"),
            Maybe.From(metadata.Version).Map(s => $"X-AppImage-Version={s}"),
        };

        return items.Compose() + "\n";
    }

    public static string RunScript(string executablePath)
    {
        return $"#!/usr/bin/env sh\n\"{executablePath}\" \"$@\"";
    }

    public static string AppStream(PackageMetadata packageMetadata)
    {
        return AppStreamXmlGenerator.GenerateXml(packageMetadata).ToString();
    }
}
