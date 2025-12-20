using System.Text.Json;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Flatpak;

internal class FlatpakFactory
{
    public async Task<Result<FlatpakBuildPlan>> BuildPlan(
        IContainer applicationRoot,
        PackageMetadata metadata,
        FlatpakOptions? options = null)
    {
        var effectiveOptions = options ?? new FlatpakOptions();

        var executableResult = await BuildUtils.GetExecutable(applicationRoot, new FromDirectoryOptions());
        if (executableResult.IsFailure)
        {
            return Result.Failure<FlatpakBuildPlan>(executableResult.Error);
        }

        return await BuildPlan(applicationRoot, metadata, executableResult.Value, effectiveOptions);
    }

    public async Task<Result<FlatpakBuildPlan>> BuildPlan(
        IContainer applicationRoot,
        PackageMetadata metadata,
        INamedByteSourceWithPath executable,
        FlatpakOptions? options = null)
    {
        var effectiveOptions = options ?? new FlatpakOptions();
        var appId = metadata.Id.GetValueOrDefault($"com.example.{metadata.Package}");
        var commandName = effectiveOptions.CommandOverride.GetValueOrDefault(appId);
        var executableTargetPath = ((INamedWithPath)executable).FullPath().ToString().Replace("\\", "/");

        return await BuildPlanInternal(
            applicationRoot,
            metadata,
            executable,
            effectiveOptions,
            appId,
            commandName,
            executableTargetPath);
    }

    private async Task<Result<FlatpakBuildPlan>> BuildPlanInternal(
        IContainer applicationRoot,
        PackageMetadata metadata,
        INamedByteSourceWithPath executable,
        FlatpakOptions options,
        string appId,
        string commandName,
        string executableTargetPath)
    {
        // Generate the metadata file
        var metadataContent = GenerateMetadata(metadata, options, appId, commandName);

        // Generate the .desktop file (Exec should be the appId) and force Icon to appId
        var desktopFile = TextTemplates.DesktopFileContents(appId, metadata, appId);

        // Generate the appdata.xml file  
        var appDataXml = TextTemplates.AppStream(metadata);

        // Collect all application files under files/ preserving original layout
        var applicationFiles = new Dictionary<string, IByteSource>();
        foreach (var res in applicationRoot.ResourcesWithPathsRecursive())
        {
            var full = ((INamedWithPath)res).FullPath().ToString().Replace("\\", "/");
            var targetPath = $"files/{full}";
            applicationFiles[targetPath] = res;
        }
        // Add wrapper with commandName -> actual executable (kept at original location)
        applicationFiles[$"files/bin/{commandName}"] = ByteSource.FromString(TextTemplates.RunScript($"/app/{executableTargetPath}"));

        // Add desktop file under files/share so build-export can pick it
        var desktopFileName = $"{appId}.desktop";
        var desktopTargetPath = $"files/share/applications/{desktopFileName}";
        applicationFiles[desktopTargetPath] = ByteSource.FromString(desktopFile);

        // Add AppStream metadata under files/share (use .metainfo.xml as per spec)
        var appDataFileName = $"{appId}.metainfo.xml";  
        var appDataTargetPath = $"files/share/metainfo/{appDataFileName}";
        applicationFiles[appDataTargetPath] = ByteSource.FromString(appDataXml);

        // Add icons under files/share
        foreach (var iconFile in metadata.IconFiles)
        {
            var key = iconFile.Key.Replace("\\", "/");
            var iconTargetPath = key.StartsWith("usr/share/")
                ? key.Replace("usr/share/", "files/share/")
                : $"files/share/{key}";
            // Ensure icon filename matches appId so build-export picks it up
            var lastSlash = iconTargetPath.LastIndexOf('/') + 1;
            var dir = iconTargetPath.Substring(0, lastSlash);
            var ext = System.IO.Path.GetExtension(iconTargetPath);
            var renamed = $"{dir}{appId}{ext}";
            applicationFiles[renamed] = iconFile.Value;
        }

        // Add metadata file at root
        applicationFiles["metadata"] = ByteSource.FromString(metadataContent);

        var layoutResult = applicationFiles.ToRootContainer();
        if (layoutResult.IsFailure)
        {
            return Result.Failure<FlatpakBuildPlan>(layoutResult.Error);
        }

        return Result.Success(new FlatpakBuildPlan(
            commandName,
            executableTargetPath, 
            appId,
            metadata,
            layoutResult.Value));
    }

    private string GenerateMetadata(PackageMetadata metadata, FlatpakOptions options, string appId, string commandName)
    {
        var architecture = options.ArchitectureOverride.GetValueOrDefault(metadata.Architecture);
        
        var lines = new List<string>
        {
            "[Application]",
            $"name={appId}",
            $"runtime={options.Runtime}/{FlatpakArchitectureName(architecture)}/{options.RuntimeVersion}",
            $"sdk={options.Sdk}/{FlatpakArchitectureName(architecture)}/{options.RuntimeVersion}",
            $"branch={options.Branch}",
            $"command={commandName}",
            "",
            "[Context]"
        };

        if (options.Shared.Any())
        {
            lines.Add($"shared={string.Join(";", options.Shared)};");
        }

        if (options.Sockets.Any())
        {
            lines.Add($"sockets={string.Join(";", options.Sockets)};");
        }

        if (options.Devices.Any()) 
        {
            lines.Add($"devices={string.Join(";", options.Devices)};");
        }

        if (options.Filesystems.Any())
        {
            lines.Add($"filesystems={string.Join(";", options.Filesystems)};");
        }

        return string.Join("\n", lines);
    }

    private string FlatpakArchitectureName(Architecture architecture)
    {
        return architecture.PackagePrefix; // x86_64, aarch64, etc.
    }
}
