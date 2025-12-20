using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.Artifacts;

public sealed record ExeBuildArtifacts(IByteSource Installer, IByteSource Uninstaller)
{
    public async Task<Result> WriteTo(FileInfo installerPath, FileInfo uninstallerPath)
    {
        var installerResult = await Installer.WriteTo(installerPath.FullName);
        var uninstallerResult = await Uninstaller.WriteTo(uninstallerPath.FullName);
        return Result.Combine(installerResult, uninstallerResult);
    }
}
