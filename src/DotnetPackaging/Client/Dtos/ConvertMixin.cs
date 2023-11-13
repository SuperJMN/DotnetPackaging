using DotnetPackaging.Archives.Deb;
using DotnetPackaging.Common;
using Zafiro.FileSystem;

namespace DotnetPackaging.Client.Dtos;

public static class ConvertMixin
{
    public static PackageDefinition ToModel(this PackageDefinitionDto dto) => new(dto.PackageMetadata.ToModel(), dto.Executables.ToModel());

    private static Dictionary<ZafiroPath, ExecutableMetadata> ToModel(this IDictionary<string, ExecutableMetadataDto> dto)
    {
        return dto.ToDictionary(x => (ZafiroPath)x.Key, x => x.Value.ToModel());
    }

    private static ExecutableMetadata ToModel(this ExecutableMetadataDto dto) => new(dto.CommandName, dto.DesktopEntry.ToModel());

    private static DesktopEntry ToModel(this DesktopEntryDto dto)
    {
        var iconDatas = dto.Icons
            .Select(
                pair => new IconData(pair.Key, Image.Load(pair.Value)));

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