using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public static class DataMixin
{
    public static Func<Task<Result<Stream>>> ToStreamFactory(this DesktopMetadata desktopEntry)
    {
        var textContent = $"""
                           [Desktop Entry]
                           Type=Application
                           Name={desktopEntry.Name}
                           Path={desktopEntry.Name}
                           StartupWMClass={desktopEntry.StartupWmClass}
                           GenericName={desktopEntry.StartupWmClass}
                           Comment={desktopEntry.Comment}
                           Icon={desktopEntry.Name}
                           Terminal=false
                           Exec={desktopEntry.ExecutablePath}
                           Categories={desktopEntry.Categories.Map(cats => string.Join(";", cats))};
                           Keywords={desktopEntry.Keywords.Map(keywords => string.Join(";", keywords))};
                           """.FromCrLfToLf();

        return () => Task.FromResult(Result.Success((Stream)new MemoryStream(textContent.GetAsciiBytes())));
    }
}