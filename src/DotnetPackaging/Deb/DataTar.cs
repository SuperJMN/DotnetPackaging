using System.Reactive.Linq;
using DotnetPackaging.Common;
using DotnetPackaging.Tar;
using Zafiro;
using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

public class DataTar
{
    private readonly Contents contents;
    private readonly Metadata metadata;

    public DataTar(Metadata metadata, Contents contents)
    {
        this.metadata = metadata;
        this.contents = contents;
    }

    private ZafiroPath Root => "./usr/local/bin";
    private ZafiroPath IconsRoot => "./usr/share/icons/hicolor";
    private ZafiroPath ApplicationsRoot => "./usr/share/applications";
    private ZafiroPath PackageRoot => Root.Combine(metadata.PackageName);

    public TarFile Tar
    {
        get
        {
            var entries = GetAllEntries(FileEntries());
            var entryDatas = entries.ToArray();
            return new(entryDatas);
        }
    }

    private static IEnumerable<EntryData> GetAllEntries(IEnumerable<EntryData> fileEntries)
    {
        return fileEntries
            .SelectMany(data => new[] { data }.Concat(DirectoryEntries(data.Name)))
            .Distinct(new LambdaComparer<EntryData>((a, b) => Equals(a.Name, b.Name)))
            .OrderByDescending(x => x.Properties.LinkIndicator)
            .ThenBy(x => x.Name.Length);
    }

    private static IEnumerable<EntryData> DirectoryEntries(ZafiroPath filePath)
    {
        return filePath.Parents().Select(path => DirectoryEntry(path));
    }

    public IEnumerable<EntryData> FileEntries() => PackageContents().Concat(ApplicationEntries());

    private IEnumerable<EntryData> ApplicationEntries()
    {
        return contents.Entries
            .OfType<ExecutableContent>()
            .SelectMany(GetDesktopEntries);
    }

    private IEnumerable<EntryData> GetDesktopEntries(ExecutableContent executableContent)
    {
        var desktopEntry = DesktopEntry(executableContent);
        var appEntry = RootExecutable(executableContent);
        var iconEntries = executableContent.Icons.Icons.Select(data => CreateIconEntry(executableContent, data));

        return new[] { desktopEntry, appEntry }.Concat(iconEntries);
    }

    private EntryData DesktopEntry(ExecutableContent executableContent)
    {
        var path = ApplicationsRoot.Combine(executableContent.Name + ".desktop");

        var shortcut = $"""
                        [Desktop Entry]
                        Type=Application
                        Name={executableContent.Name}
                        StartupWMClass={executableContent.StartupWmClass}
                        GenericName={executableContent.StartupWmClass}
                        Comment=Privacy focused Bitcoin wallet.
                        Icon={metadata.PackageName}
                        Terminal=false
                        Exec={executableContent.Path}
                        Categories=Office;Finance;
                        Keywords=bitcoin;wallet;crypto;blockchain;wasabi;privacy;anon;awesome;
                        """.FromCrLfToLf();

        return new EntryData(path, new Properties
        {
            GroupName = "root",
            OwnerUsername = "root",
            Length = shortcut.Length,
            FileMode = FileMode.Parse("755"),
            GroupId = 1000,
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            LinkIndicator = 0
        }, () => shortcut.GetAsciiBytes().ToObservable());
    }

    private EntryData CreateIconEntry(ExecutableContent executableContent, IconData iconData)
    {
        var path = IconsRoot.Combine($"{iconData.TargetSize}x{iconData.TargetSize}/apps/{executableContent.Name}.png");
        var properties = new Properties
        {
            FileMode = FileMode.Parse("775"),
            Length = iconData.IconBytes().ToEnumerable().Count(),
            GroupId = 1000,
            GroupName = "root",
            LastModification = DateTimeOffset.Now,
            LinkIndicator = 0,
            OwnerId = 1000,
            OwnerUsername = "root"
        };
        return new EntryData(path, properties, iconData.IconBytes);
    }

    private EntryData RootExecutable(ExecutableContent executableContent)
    {
        // TODO: Optimize length retrieval
        var path = ZafiroPath.Create(Root).Value.Combine(executableContent.Path);

        var length = GetExecEntry(executableContent.Path).ToEnumerable().Count();

        return new EntryData(path, new Properties
        {
            GroupName = "root",
            OwnerUsername = "root",
            Length = length,
            FileMode = FileMode.Parse("755"),
            GroupId = 1000,
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            LinkIndicator = 0
        }, () => GetExecEntry(executableContent.Path));
    }

    private IObservable<byte> GetExecEntry(ZafiroPath pathToExecutable)
    {
        var text = $"""
                    #!/usr/bin/env sh
                    {PackageRoot}/{pathToExecutable} $@

                    """.FromCrLfToLf();

        return text.GetAsciiBytes().ToObservable();
    }

    private IEnumerable<EntryData> PackageContents()
    {
        var entryDatas = contents.Entries.Select(content =>
        {
            var length = content.Bytes().ToEnumerable().Count();

            return new EntryData(PackageRoot.Combine(content.Path), new Properties
            {
                GroupName = "root",
                OwnerUsername = "root",
                Length = length,
                FileMode = FileMode.Parse("644"),
                GroupId = 1000,
                LastModification = DateTimeOffset.Now,
                OwnerId = 1000,
                LinkIndicator = 0
            }, content.Bytes);
        });

        return entryDatas;
    }

    private static Tar.EntryData DirectoryEntry(string path) => new(path, new Tar.Properties()
    {
        Length = 0,
        GroupName = "root",
        OwnerUsername = "root",
        GroupId = 1000,
        OwnerId = 1000,
        FileMode = FileMode.Parse("644"),
        LastModification = DateTimeOffset.Now,
        LinkIndicator = 5,
    }, Observable.Empty<byte>);
}