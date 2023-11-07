using System.Reactive.Linq;
using DotnetPackaging.Deb;
using Zafiro.FileSystem;
using Zafiro.IO;

namespace DotnetPackage.Console.Dtos;

public static class ConvertMixin
{
    public static Packaging ToModel(this PackagingDto dto) => new(dto.PackageMetadata.ToModel(), dto.Executables.ToModel());

    private static Dictionary<ZafiroPath, ExecutableMetadata> ToModel(this IDictionary<string, ExecutableMetadataDto> dto)
    {
        return dto.ToDictionary(x => (ZafiroPath)x.Key, x => x.Value.ToModel());
    }

    private static ExecutableMetadata ToModel(this ExecutableMetadataDto dto) => new(dto.CommandName, dto.DesktopEntry.ToModel());

    private static DesktopEntry ToModel(this DesktopEntryDto dto)
    {
        var iconDatas = dto.Icons
            .Select(
                pair => new IconData(pair.Key,
                    () => Observable.Using(() => File.OpenRead(pair.Value), stream => stream.ToObservable())));

        var iconsResources = IconResources.Create(iconDatas.ToArray());

        return new DesktopEntry
        {
            Icons = iconsResources.Value,
            Name = dto.Name,
            StartupWmClass = dto.StartupWmClass,
            Keywords = dto.Keywords,
            Comment = dto.Comment,
            Categories = dto.Categories
        };
    }

    private static Metadata ToModel(this MetadataDto dto) => new()
    {
        Maintainer = dto.Maintainer,
        PackageName = dto.PackageName,
        ApplicationName = dto.ApplicationName,
        Architecture = dto.Architecture,
        Homepage = dto.Homepage,
        License = dto.License,
        Description = dto.Description,
        Version = dto.Version
    };
}