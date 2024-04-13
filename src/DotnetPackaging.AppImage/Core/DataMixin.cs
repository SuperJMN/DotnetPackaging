using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public static class DataMixin
{
    public static Func<Task<Result<Stream>>> ToStreamFactory(this DesktopMetadata desktopEntry)
    {
        var textContent = $"""
                           [Desktop Entry]
                           Type=Application
                           Path={desktopEntry.Name}
                           StartupWMClass={desktopEntry.StartupWmClass}
                           GenericName={desktopEntry.StartupWmClass}
                           Comment={desktopEntry.Comment}
                           Icon={desktopEntry.Name}
                           Terminal=false
                           Exec={desktopEntry.ExecutablePath}
                           Categories={string.Join(";", desktopEntry.Categories)};
                           Keywords={string.Join(";", desktopEntry.Keywords)};
                           """.FromCrLfToLf();

        return () => Task.FromResult(Result.Success((Stream)new MemoryStream(textContent.GetAsciiBytes())));
    }
}