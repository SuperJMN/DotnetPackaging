using DotnetPackaging.Archives.Deb;
using DotnetPackaging.Common;
using SixLabors.ImageSharp;
using Zafiro.FileSystem;

namespace DotnetPackaging.Tests.Deb.EndToEnd;

public static class TestData
{
    private static async Task<DesktopEntry> GetDesktopEntry()
    {
        var data = await IconData.Create(32, await Image.LoadAsync("TestFiles\\icon.png"));

        return new DesktopEntry()
        {
            Name = "Avalonia Syncer",
            Icons = IconResources.Create(data).Value,
            StartupWmClass = "AvaloniaSyncer",
            Keywords = new[] { "file manager" },
            Comment = "The best file explorer ever",
            Categories = new[] { "FileManager", "Filesystem", "Utility", "FileTransfer", "Archiving" }
        };
    }

    public static Metadata Metadata => new()
    {
        PackageName = "AvaloniaSyncer",
        Description = "Best file explorer you'll ever find",
        ApplicationName = "Avalonia Syncer",
        Architecture = "amd64",
        Homepage = "https://www.something.com",
        License = "MIT",
        Maintainer = "developer@mail.com",
        Version = "0.1.33"
    };

    public static async Task<Dictionary<ZafiroPath, ExecutableMetadata>> GetExecutableFiles() => new()
    {
        ["TestDebpackage.Desktop"] = new("avaloniasyncer", await GetDesktopEntry()),
    };

    public static async Task<PackageDefinition> GetPackageDefinition() => new(Metadata, await GetExecutableFiles());
}