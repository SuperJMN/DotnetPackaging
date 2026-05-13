using CSharpFunctionalExtensions;
using DotnetProjectKit;
using DotnetPackaging;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.Metadata;

public sealed class ExePackagerMetadata
{
    public Options Options { get; set; } = new();
    public Maybe<string> OutputName { get; set; } = Maybe<string>.None;
    public Maybe<string> ProjectName { get; set; } = Maybe<string>.None;
    public Maybe<FileInfo> ProjectFile { get; set; } = Maybe<FileInfo>.None;
    public Maybe<string> Vendor { get; set; } = Maybe<string>.None;
    public Maybe<string> RuntimeIdentifier { get; set; } = Maybe<string>.None;
    public Maybe<IByteSource> Stub { get; set; } = Maybe<IByteSource>.None;
    public Maybe<IByteSource> SetupLogo { get; set; } = Maybe<IByteSource>.None;
    public Maybe<ApplicationInfo> ApplicationInfo { get; set; } = Maybe<ApplicationInfo>.None;
    public Maybe<string> PfxPath { get; set; } = Maybe<string>.None;
    public Maybe<string> PfxPassword { get; set; } = Maybe<string>.None;
}
