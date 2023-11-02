using System.Reactive.Linq;
using DotnetPackaging.Common;
using DotnetPackaging.Tar;
using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

public class DataTar
{
    private readonly Contents contents;
    private readonly string packageName;

    public DataTar(string packageName, Contents contents)
    {
        this.packageName = packageName;
        this.contents = contents;
    }

    private ZafiroPath Root => "./usr/local/bin";
    private ZafiroPath PackageRoot => $"{Root}/{packageName}";

    public TarFile Tar => new(Entries().ToArray());

    public IEnumerable<EntryData> Entries() => Explicit().Concat(Executables()).Concat(Metadata());

    private IEnumerable<EntryData> Metadata() => Enumerable.Empty<EntryData>();

    private IEnumerable<EntryData> Executables()
    {
        var entryDatas = contents.Entries.Where(tuple => tuple.Item2.IsExecutable)
            .Select(tuple =>
        {
            // TODO: Optimize length retrieval
            var path = ZafiroPath.Create(Root).Value.Combine(tuple.Item1);

            return new EntryData(path, new Properties
            {
                GroupName = "root",
                OwnerUsername = "root",
                Length = GetExecEntry(tuple.Item1).ToEnumerable().Count(),
                FileMode = FileMode.Parse("755"),
                GroupId = 1000,
                LastModification = DateTimeOffset.Now,
                OwnerId = 1000,
                LinkIndicator = 0
            },  () => GetExecEntry(tuple.Item1));
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

            return new EntryData(path, new Properties
            {
                GroupName = "root",
                OwnerUsername = "root",
                Length = tuple.Item2.Bytes().ToEnumerable().Count(),
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