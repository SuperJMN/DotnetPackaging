using CSharpFunctionalExtensions;
using DotnetPackaging;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Dmg;

public sealed class DmgPackagerMetadata
{
    public Maybe<string> VolumeName { get; set; } = Maybe<string>.None;
    public Maybe<string> ExecutableName { get; set; } = Maybe<string>.None;
    public Maybe<bool> Compress { get; set; } = Maybe<bool>.None;
    public Maybe<bool> AddApplicationsSymlink { get; set; } = Maybe<bool>.None;
    public Maybe<bool> IncludeDefaultLayout { get; set; } = Maybe<bool>.None;
    public Maybe<IIcon> Icon { get; set; } = Maybe<IIcon>.None;
    public Maybe<IByteSource> InfoPlist { get; set; } = Maybe<IByteSource>.None;
    public Maybe<string> BundleIdentifier { get; set; } = Maybe<string>.None;
    public Maybe<string> BundleVersion { get; set; } = Maybe<string>.None;
}
