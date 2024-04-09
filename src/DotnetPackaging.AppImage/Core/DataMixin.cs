using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;
using DotnetPackaging.Common;

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

    public static async Task<DesktopMetadata> FromStreamFactory(this Func<Task<Result<Stream>>> streamFactory)
    {
        var result = await streamFactory();
        if (!result.IsSuccess)
        {
            throw new Exception("Failed to get stream");
        }
        using var stream = result.Value;
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        var lines = content.Split('\n');
        string name = null, startupWmClass = null, comment = null, executablePath = null;
        IEnumerable<string> categories = null, keywords = null;
        foreach (var line in lines)
        {
            var parts = line.Split('=');
            if (parts.Length != 2) continue;
            var key = parts[0].Trim();
            var value = parts[1].Trim();
            switch (key)
            {
                case "Path":
                case "Icon":
                    name = value;
                    break;
                case "StartupWMClass":
                    startupWmClass = value;
                    break;
                case "Comment":
                    comment = value;
                    break;
                case "Exec":
                    executablePath = value;
                    break;
                case "Categories":
                    categories = value.Split(';').ToList();
                    break;
                case "Keywords":
                    keywords = value.Split(';').ToList();
                    break;
            }
        }
        return new DesktopMetadata
        {
            Name = name,
            StartupWmClass = startupWmClass,
            Comment = comment,
            ExecutablePath = executablePath,
            Categories = categories,
            Keywords = keywords
        };
    }
}