﻿using System.Reactive.Linq;
using CSharpFunctionalExtensions;
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
            return new TarFile(entryDatas);
        }
    }

    public IEnumerable<EntryData> FileEntries() => PackageContents().Concat(ApplicationEntries());

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

    private static EntryData DirectoryEntry(string path) => new(path, new Properties
    {
        Length = 0,
        GroupName = "root",
        OwnerUsername = "root",
        GroupId = 1000,
        OwnerId = 1000,
        FileMode = FileMode.Parse("777"),
        LastModification = DateTimeOffset.Now,
        LinkIndicator = 5
    }, Observable.Empty<byte>);

    private IEnumerable<EntryData> ApplicationEntries() => contents
        .OfType<ExecutableContent>()
        .SelectMany(GetDesktopEntries);

    private IEnumerable<EntryData> GetDesktopEntries(ExecutableContent executableContent)
    {
        var desktopEntry = executableContent.DesktopEntry.Map(entry => ExecutableEntries(executableContent, entry)).GetValueOrDefault(Enumerable.Empty<EntryData>());
        var appEntry = RootExecutable(executableContent);

        return new[] { appEntry }.Concat(desktopEntry);
    }

    private IEnumerable<EntryData> ExecutableEntries(ExecutableContent executableContent, DesktopEntry desktopEntry)
    {
        return new[] { DesktopEntry(executableContent, desktopEntry) }.Concat(IconEntries(desktopEntry));
    }

    private IEnumerable<EntryData> IconEntries(DesktopEntry entry)
    {
        return entry.Icons.Icons.Select(data => IconEntry(entry, data));
    }

    private EntryData DesktopEntry(ExecutableContent executableContent, DesktopEntry entry)
    {
        var path = ApplicationsRoot.Combine(entry.Name + ".desktop");

        var shortcut = $"""
                        [Desktop Entry]
                        Type=Application
                        Name={entry.Name}
                        StartupWMClass={entry.StartupWmClass}
                        GenericName={entry.StartupWmClass}
                        Comment=Privacy focused Bitcoin wallet.
                        Icon={entry.Name}
                        Terminal=false
                        Exec={executableContent.Path}
                        Categories=Office;Finance;
                        Keywords={string.Join(";", entry.Keywords)};
                        """.FromCrLfToLf();

        return new EntryData(path, new Properties
        {
            GroupName = "root",
            OwnerUsername = "root",
            Length = shortcut.Length,
            FileMode = FileMode.Parse("644"),
            GroupId = 1000,
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            LinkIndicator = 0
        }, () => shortcut.GetAsciiBytes().ToObservable());
    }

    private EntryData IconEntry(DesktopEntry desktopEntry, IconData iconData)
    {
        var path = IconsRoot.Combine($"{iconData.TargetSize}x{iconData.TargetSize}/apps/{desktopEntry.Name}.png");
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
        var path = ZafiroPath.Create(Root).Value.Combine(executableContent.CommandName);

        var length = GetExecEntry(executableContent.CommandName).ToEnumerable().Count();

        return new EntryData(path, new Properties
        {
            GroupName = "root",
            OwnerUsername = "root",
            Length = length,
            FileMode = FileMode.Parse("777"),
            GroupId = 1000,
            LastModification = DateTimeOffset.Now,
            OwnerId = 1000,
            LinkIndicator = 0
        }, () => GetExecEntry(executableContent.Path));
    }

    private IObservable<byte> GetExecEntry(string commandName)
    {
        var dotLessPackageRootPath = PackageRoot.ToString()[1..];

        var text = $"""
                    #!/usr/bin/env sh
                    {dotLessPackageRootPath}/{commandName} $@

                    """.FromCrLfToLf();

        return text.GetAsciiBytes().ToObservable();
    }

    private IEnumerable<EntryData> PackageContents()
    {
        var entryDatas = contents.Select(content =>
        {
            var length = content.Bytes().ToEnumerable().Count();

            return new EntryData(PackageRoot.Combine(content.Path), new Properties
            {
                GroupName = "root",
                OwnerUsername = "root",
                Length = length,
                FileMode = content is RegularContent ? FileMode.Parse("644") : FileMode.Parse("751"),
                GroupId = 1000,
                LastModification = DateTimeOffset.Now,
                OwnerId = 1000,
                LinkIndicator = 0
            }, content.Bytes);
        });

        return entryDatas;
    }
}