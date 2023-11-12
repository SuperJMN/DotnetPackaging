using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Archives.Deb.Contents;
using DotnetPackaging.Archives.Tar;
using DotnetPackaging.Common;
using Zafiro;
using Zafiro.FileSystem;

namespace DotnetPackaging.Archives.Deb;

public class DataFactory
{
    private readonly ContentCollection contentCollection;
    private readonly Metadata metadata;

    public DataFactory(Metadata metadata, ContentCollection contentCollection)
    {
        this.metadata = metadata;
        this.contentCollection = contentCollection;
    }

    private ZafiroPath Root => "./usr/local/bin";
    private ZafiroPath IconsRoot => "./usr/share/icons/hicolor";
    private ZafiroPath ApplicationsRoot => "./usr/share/applications";
    private ZafiroPath PackageRoot => new ZafiroPath("./usr/share").Combine(metadata.PackageName);

    public TarFile Tar
    {
        get
        {
            var entries = GetAllEntries(FileEntries());
            var array = entries.ToArray();
            return new TarFile(array);
        }
    }

    public IEnumerable<Entry> FileEntries() => PackageContents().Concat(ApplicationEntries());

    private static IEnumerable<Entry> GetAllEntries(IEnumerable<Entry> fileEntries)
    {
        return fileEntries
            .SelectMany(data => new[] { data }.Concat(DirectoryEntries(data.Name)))
            .Distinct(new LambdaComparer<Entry>((a, b) => Equals(a.Name, b.Name)))
            .OrderByDescending(x => x.Properties.LinkIndicator)
            .ThenBy(x => x.Name.Length);
    }

    private static IEnumerable<Entry> DirectoryEntries(ZafiroPath filePath)
    {
        return filePath.Parents().Select(path => DirectoryEntry(path));
    }

    private static Entry DirectoryEntry(string path) => new(path, new Properties
    {
        GroupName = "root",
        OwnerUsername = "root",
        GroupId = 1000,
        OwnerId = 1000,
        FileMode = FileMode.Parse("777"),
        LastModification = DateTimeOffset.Now,
        LinkIndicator = 5
    }, new ByteFlow(Observable.Empty<byte>(), 0));

    private IEnumerable<Entry> ApplicationEntries() => contentCollection
        .OfType<ExecutableContent>()
        .SelectMany(GetDesktopEntries);

    private IEnumerable<Entry> GetDesktopEntries(ExecutableContent executableContent)
    {
        var desktopEntries = executableContent.DesktopEntry.Map(entry => DesktopEntries(executableContent, entry)).GetValueOrDefault(Enumerable.Empty<Entry>());
        var appEntry = RootExecutable(executableContent);

        return new[] { appEntry }.Concat(desktopEntries);
    }

    private IEnumerable<Entry> DesktopEntries(ExecutableContent executableContent, DesktopEntry desktopEntry)
    {
        return new[] { DesktopEntry(executableContent, desktopEntry) }.Concat(IconEntries(desktopEntry));
    }

    private IEnumerable<Entry> IconEntries(DesktopEntry entry)
    {
        return entry.Icons.Icons.Select(data => IconEntry(entry, data));
    }

    private Entry DesktopEntry(ExecutableContent executableContent, DesktopEntry desktopEntry)
    {
        var path = ApplicationsRoot.Combine(desktopEntry.Name + ".desktop");

        var shortcut = $"""
                        [Desktop Entry]
                        Type=Application
                        Name={desktopEntry.Name}
                        StartupWMClass={executableContent.Path.Name()}
                        GenericName={desktopEntry.StartupWmClass}
                        Comment={desktopEntry.Comment}
                        Icon={desktopEntry.Name}
                        Terminal=false
                        Exec={executableContent.CommandName}
                        Categories={string.Join(";", desktopEntry.Categories)};
                        Keywords={string.Join(";", desktopEntry.Keywords)};
                        """.FromCrLfToLf();

        return new Entry(path, new Properties
        {
            GroupName = "root",
            OwnerUsername = "root",
            FileMode = FileMode.Parse("644"),
            GroupId = 1000,
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            LinkIndicator = 0
        }, shortcut.ToByteFlow(Encoding.ASCII));
    }

    private Entry IconEntry(DesktopEntry desktopEntry, IconData iconData)
    {
        var path = IconsRoot.Combine($"{iconData.TargetSize}x{iconData.TargetSize}/apps/{desktopEntry.Name}.png");
        var properties = new Properties
        {
            FileMode = FileMode.Parse("775"),
            GroupId = 1000,
            GroupName = "root",
            LastModification = DateTimeOffset.Now,
            LinkIndicator = 0,
            OwnerId = 1000,
            OwnerUsername = "root"
        };

        return new Entry(path, properties, iconData);
    }

    private Entry RootExecutable(ExecutableContent executableContent)
    {
        // TODO: Optimize length retrieval
        var path = ZafiroPath.Create(Root).Value.Combine(executableContent.CommandName);

        var execEntry = GetExecEntry(executableContent);
        var length = execEntry.ToEnumerable().Count();

        return new Entry(path, new Properties
        {
            GroupName = "root",
            OwnerUsername = "root",
            FileMode = FileMode.Parse("777"),
            GroupId = 1000,
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            LinkIndicator = 0
        }, new ByteFlow(execEntry, length));
    }

    private IObservable<byte> GetExecEntry(ExecutableContent executableContent)
    {
        var dotLessPackageRootPath = PackageRoot.ToString()[1..];
        var fullExePath = $"{dotLessPackageRootPath}/{executableContent.Path}";

        var text = $"""
                    #!/usr/bin/env sh
                    {fullExePath} $@

                    """.FromCrLfToLf();

        var asciiBytes = text.GetAsciiBytes();
        return asciiBytes.ToObservable();
    }

    private IEnumerable<Entry> PackageContents()
    {
        var entries = contentCollection.Select(content => new Entry(PackageRoot.Combine(content.Path), new Properties
        {
            GroupName = "root",
            OwnerUsername = "root",
            FileMode = content is RegularContent ? FileMode.Parse("644") : FileMode.Parse("751"),
            GroupId = 1000,
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            LinkIndicator = 0
        }, content));

        return entries;
    }
}