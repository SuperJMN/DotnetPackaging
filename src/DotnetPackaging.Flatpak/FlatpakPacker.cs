using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Flatpak;

public class FlatpakPacker
{
    // Build a plan with sensible defaults from a container only
    public async Task<Result<FlatpakBuildPlan>> Plan(IContainer applicationRoot, FromDirectoryOptions? setup = null, FlatpakOptions? options = null)
    {
        var effectiveSetup = setup ?? new FromDirectoryOptions();
        var execResult = await BuildUtils.GetExecutable(applicationRoot, effectiveSetup);
        if (execResult.IsFailure) return Result.Failure<FlatpakBuildPlan>(execResult.Error);
        var archResult = await BuildUtils.GetArch(effectiveSetup, execResult.Value);
        if (archResult.IsFailure) return Result.Failure<FlatpakBuildPlan>(archResult.Error);
        var pm = await BuildUtils.CreateMetadata(effectiveSetup, applicationRoot, archResult.Value, execResult.Value, effectiveSetup.IsTerminal, Maybe<string>.None);
        return await new FlatpakFactory().BuildPlan(applicationRoot, pm, options);
    }

    // Bundle using system flatpak if available; fallback to internal OSTree bundle
    public async Task<Result<IByteSource>> Bundle(IContainer applicationRoot, FromDirectoryOptions? setup = null, FlatpakOptions? options = null, bool preferSystem = true)
    {
        var planResult = await Plan(applicationRoot, setup, options);
        if (planResult.IsFailure) return Result.Failure<IByteSource>(planResult.Error);
        var plan = planResult.Value;

        if (preferSystem)
        {
            var tmpAppDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-flatpak-app-" + Guid.NewGuid());
            var tmpRepoDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dp-flatpak-repo-" + Guid.NewGuid());
            Directory.CreateDirectory(tmpAppDir);
            Directory.CreateDirectory(tmpRepoDir);

            var write = await plan.ToRootContainer().WriteTo(tmpAppDir);
            if (write.IsFailure) return Result.Failure<IByteSource>(write.Error);

            TryMakeExecutable(System.IO.Path.Combine(tmpAppDir, "files", "bin", plan.CommandName));
            TryMakeExecutable(System.IO.Path.Combine(tmpAppDir, "files", plan.ExecutableTargetPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));

            var arch = plan.Metadata.Architecture.PackagePrefix;
            var appId = plan.AppId;
            var finish = Run("flatpak", $"build-finish \"{tmpAppDir}\" --command={plan.CommandName}");
            if (finish.IsSuccess)
            {
                var export = Run("flatpak", $"build-export --arch={arch} \"{tmpRepoDir}\" \"{tmpAppDir}\" {options?.Branch ?? "stable"}");
                if (export.IsSuccess)
                {
                    var outFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{appId}_{plan.Metadata.Version}_{arch}.flatpak");
                    var bundle = Run("flatpak", $"build-bundle \"{tmpRepoDir}\" \"{outFile}\" {appId} {options?.Branch ?? "stable"} --arch={arch}");
                    if (bundle.IsSuccess)
                    {
                        var bytes = await File.ReadAllBytesAsync(outFile);
                        return Result.Success(ByteSource.FromBytes(bytes));
                    }
                }
            }
            // fallthrough to internal if any step failed
        }

        return FlatpakBundle.CreateOstree(plan);
    }

    private static void TryMakeExecutable(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                           UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                           UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
                File.SetUnixFileMode(path, mode);
            }
        }
        catch { }
    }

    private static Result Run(string fileName, string arguments)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi)!;
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                return Result.Failure($"{fileName} {arguments}: {p.ExitCode}\n{p.StandardError.ReadToEnd()}\n{p.StandardOutput.ReadToEnd()}");
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}