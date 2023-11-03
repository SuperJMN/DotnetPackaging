using System.Reactive.Linq;
using DotnetPackaging.Common;
using DotnetPackaging.Tar;
using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

public class DataTar
{
    private readonly Contents contents;
    private readonly IconResources iconResources;
    private readonly string packageName;

    public DataTar(string packageName, IconResources iconResources, Contents contents)
    {
        this.packageName = packageName;
        this.iconResources = iconResources;
        this.contents = contents;
    }

    private ZafiroPath Root => "./usr/local/bin";
    private ZafiroPath IconsRoot => "./usr/share/icons/hicolor";
    private ZafiroPath PackageRoot => Root.Combine(packageName);

    public TarFile Tar => new(Entries().ToArray());

    public IEnumerable<EntryData> Entries() => Explicit().Concat(RootExecutables()).Concat(Metadata());

    private IEnumerable<EntryData> Metadata() => iconResources.Icons.Select(CreateIconEntry);

    private EntryData CreateIconEntry(IconData iconData)
    {
        var path = IconsRoot.Combine($"{iconData.TargetSize}x{iconData.TargetSize}/apps/{packageName}.png");
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

    private IEnumerable<EntryData> RootExecutables()
    {
        var entryDatas = contents.Entries.Where(t => t.Item2 is ExecutableContent)
            .Select(tuple =>
            {
                // TODO: Optimize length retrieval
                var path = ZafiroPath.Create(Root).Value.Combine(tuple.Item1);

                var length = GetExecEntry(tuple.Item1).ToEnumerable().Count();

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
                }, () => GetExecEntry(tuple.Item1));
            });

        return entryDatas;
    }

    private IObservable<byte> GetExecEntry(ZafiroPath pathToExecutable)
    {
        var text = $"""
                    #!/usr/bin/env sh
                    {PackageRoot}/{pathToExecutable} $@

                    """.FromCrLfToLf();

        return text.GetAsciiBytes().ToObservable();
    }

    private IEnumerable<EntryData> Explicit()
    {
        var entryDatas = contents.Entries.Select(tuple =>
        {
            var path = ZafiroPath.Create(PackageRoot).Value.Combine(tuple.Item1);

            var length = tuple.Item2.Bytes().ToEnumerable().Count();

            return new EntryData(path, new Properties
            {
                GroupName = "root",
                OwnerUsername = "root",
                Length = length,
                FileMode = FileMode.Parse("644"),
                GroupId = 1000,
                LastModification = DateTimeOffset.Now,
                OwnerId = 1000,
                LinkIndicator = 0
            }, tuple.Item2.Bytes);
        });

        return entryDatas;
    }
}