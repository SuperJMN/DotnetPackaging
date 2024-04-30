using CSharpFunctionalExtensions;

namespace DotnetPackaging;

public static class MiscMixin
{
    public static string DesktopFileContents(string appDir, PackageMetadata metadata)
    {
        var textContent = $"""
                           [Desktop Entry]
                           Type=Application
                           Name={metadata.AppName}
                           StartupWMClass={metadata.StartupWmClass}
                           GenericName={metadata.AppName}
                           Comment={metadata.Comment}
                           Icon={metadata.AppName}
                           Terminal=false
                           Exec="{appDir}/{metadata.ExecutableName}"
                           Categories={metadata.Categories};
                           Keywords={metadata.Keywords.Map(keywords => string.Join((string?) ";", (IEnumerable<string?>) keywords))};
                           """.FromCrLfToLf();

        // TODO: This is only for app-image
        var final = metadata.Version.Match(version => string.Join("\n", textContent, $"X-AppImage-Version={version};"), () => textContent);

        return final;
    }

    public static string RunScript(string executablePath)
    {
        return $"#!/usr/bin/env sh\n\"{executablePath}\" \"$@\"";
    }
}