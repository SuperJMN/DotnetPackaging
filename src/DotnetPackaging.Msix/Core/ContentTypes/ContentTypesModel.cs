namespace DotnetPackaging.Msix.Core.ContentTypes;

internal record ContentTypesModel(ImmutableList<DefaultContentType> Defaults, ImmutableList<OverrideContentType> Overrides);
