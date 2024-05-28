using System.Text;
using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.DataModel;
using Zafiro.FileSystem.Unix;
using Zafiro.Mixins;

namespace DotnetPackaging.Deb.Builder;

public static class TarEntryBuilder
{
    public static IEnumerable<TarEntry> From(IDirectory directory, PackageMetadata metadata, IFile executable)
    {
        var files = directory.RootedFiles();
        var appDir = $"/opt/{metadata.Package}";
        var executablePath = $"{appDir}/{executable.Name}";
        var tarEntriesFromImplicitFiles = ImplicitFileEntries(metadata, executablePath);
        var tarEntriesFromFiles = TarEntriesFromFiles(files, metadata, executable);
        var allFiles = tarEntriesFromFiles.Concat(tarEntriesFromImplicitFiles).ToList();
        var directoryEntries = CreateDirectoryEntries(allFiles);
        var tarEntries = directoryEntries.Concat(allFiles);
        return tarEntries;
    }
    
    private static IEnumerable<FileTarEntry> ImplicitFileEntries(PackageMetadata metadata, string executablePath)
    {
        return new FileTarEntry[]
        {
            new($"./usr/local/share/applications/{metadata.Package.ToLower()}.desktop", new StringData(TextTemplates.DesktopFileContents(executablePath, metadata), Encoding.ASCII), Misc.RegularFileProperties()),
            new($"./usr/local/bin/{metadata.Package.ToLower()}", new StringData(TextTemplates.RunScript(executablePath), Encoding.ASCII), Misc.ExecutableFileProperties())
        }.Concat(GetIconEntry(metadata));
    }
    
    private static IEnumerable<FileTarEntry> GetIconEntry(PackageMetadata metadata)
    {
        if (metadata.Icon.HasNoValue)
        {
            return Enumerable.Empty<FileTarEntry>();
        }

        var size = metadata.Icon.Value.Size;
        return [new FileTarEntry($"./usr/share/icons/hicolor/{size}x{size}/apps/{metadata.Package.ToLower()}.png", metadata.Icon.Value, Misc.RegularFileProperties())];
    }
    
    private static IEnumerable<TarEntry> TarEntriesFromFiles(IEnumerable<IRootedFile> files, PackageMetadata metadata, IFile executable)
    {
        return files.Select(x => new FileTarEntry($"./opt/{metadata.Package}/" + x.FullPath(), x.Value, GetFileProperties(x, executable.Name)));
    }

    private static TarFileProperties GetFileProperties(IRootedFile file, string executableName)
    {
        if (string.Equals(file.Name, executableName, StringComparison.Ordinal))
        {
            return Misc.ExecutableFileProperties();
        }

        return Misc.RegularFileProperties();
    }

    private static IEnumerable<TarEntry> CreateDirectoryEntries(IEnumerable<TarEntry> allFiles)
    {
        var directoryProperties = new TarDirectoryProperties
        {
            FileMode = "755".ToFileMode(),
            GroupId = 1000,
            OwnerId = 1000,
            GroupName = "root",
            OwnerUsername = "root",
            LastModification = DateTimeOffset.Now
        };
        var directoryEntries = allFiles.Select(x => ((ZafiroPath)x.Path[2..]).Parents()).Flatten().Distinct().OrderBy(x => x.RouteFragments.Count());
        return directoryEntries.Select(path => new DirectoryTarEntry($"./{path}", directoryProperties));
    }
}