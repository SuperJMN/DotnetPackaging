using CSharpFunctionalExtensions;
using DotnetPackaging;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.Metadata;

public sealed class ExePackagerMetadata
{
    public Options Options { get; set; } = new();
    public Maybe<string> OutputName { get; set; } = Maybe<string>.None;
    public Maybe<string> ProjectName { get; set; } = Maybe<string>.None;
    public Maybe<string> Vendor { get; set; } = Maybe<string>.None;
    public Maybe<string> RuntimeIdentifier { get; set; } = Maybe<string>.None;
    public Maybe<IByteSource> Stub { get; set; } = Maybe<IByteSource>.None;
    public Maybe<IByteSource> SetupLogo { get; set; } = Maybe<IByteSource>.None;
    public Maybe<ProjectMetadata> ProjectMetadata { get; set; } = Maybe<ProjectMetadata>.None;
}
