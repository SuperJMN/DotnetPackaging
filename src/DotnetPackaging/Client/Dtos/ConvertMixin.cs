using DotnetPackaging.Archives.Deb;
using DotnetPackaging.Common;
using SixLabors.ImageSharp;
using Zafiro.FileSystem;

namespace DotnetPackaging.Client.Dtos;

public static class ConvertMixin
{
    public static async Task<PackageDefinition> ToModel(this PackageDefinitionDto dto) => new(dto.PackageMetadata.ToModel(), await dto.Executables.ToModel());

    private static async Task<Dictionary<ZafiroPath, ExecutableMetadata>> ToModel(this IDictionary<string, ExecutableMetadataDto> dto)
    {
        var tasks = dto.Select(async x => new { x.Key, Model = await x.Value.ToModel() });
        var results = await Task.WhenAll(tasks.ToArray());
        return results.ToDictionary(x => (ZafiroPath)x.Key, x => x.Model);
    }

    private static async Task<ExecutableMetadata> ToModel(this ExecutableMetadataDto dto) => new(dto.CommandName, await dto.DesktopEntry.ToModel());

    private static async Task<DesktopEntry> ToModel(this DesktopEntryDto dto)
    {
        var tasks = dto.Icons.Select(async pair => await IconData.Create(pair.Key, Image.Load(pair.Value)));
        var iconDatas = await Task.WhenAll(tasks.ToArray());

        var iconsResources = IconResources.Create(iconDatas);

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