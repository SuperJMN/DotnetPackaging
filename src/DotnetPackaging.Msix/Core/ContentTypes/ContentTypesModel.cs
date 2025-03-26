using System.Collections.Immutable;

namespace MsixPackaging.Core.ContentTypes;

public record ContentTypesModel(ImmutableList<DefaultContentType> Defaults, ImmutableList<OverrideContentType> Overrides);