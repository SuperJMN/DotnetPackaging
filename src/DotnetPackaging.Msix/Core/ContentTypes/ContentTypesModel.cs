namespace DotnetPackaging.Msix.Core.ContentTypes;

public record ContentTypesModel(ImmutableList<DefaultContentType> Defaults, ImmutableList<OverrideContentType> Overrides);