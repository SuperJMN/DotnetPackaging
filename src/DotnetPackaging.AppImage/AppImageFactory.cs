using System.Reactive.Linq;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Metadata;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage;

public class AppImageFactory
{
    public async Task<Result<AppImageContainer>> Create(
        IContainer applicationRoot,
        AppImageMetadata appImageMetadata)
    {
        Task<Result<Executable>> exectTask = from exec in GetExecutable(applicationRoot)
            from arch in exec.GetArchitecture()
            select new Executable
            {
                Resource = exec,
                Architecture = arch,
            };

        Result<Executable> executableResult = await exectTask;

        return await executableResult.Bind(exec =>
        {
            var executableName = exec.Resource.Name;
            
            var desktopFile = appImageMetadata.ToDesktopFile($"/usr/bin/{executableName}");

            var appRunContent = ByteSource.FromString($"#!/bin/bash\nexec \"$APPDIR/usr/bin/{executableName}\" \"$@\"");
            var desktopContent = ByteSource.FromString(MetadataGenerator.DesktopFileContents(desktopFile));
            var appdataContent = ByteSource.FromString(MetadataGenerator.AppStreamXml(appImageMetadata.ToAppStream()));

            // Get all application files with their relative paths
            var namedByteSourceWithPaths = applicationRoot.FilesWithPathsRecursive();
            
            var applicationFiles = namedByteSourceWithPaths
                .ToDictionary(
                    file => $"usr/bin/{file.FullPath()}", IByteSource (file) => file);

            var files = new Dictionary<string, IByteSource>
            {
                ["AppRun"] = appRunContent,
                [appImageMetadata.DesktopFileName] = desktopContent,
                [$"usr/share/metainfo/{appImageMetadata.AppDataFileName}"] = appdataContent,
            };

            // Add all application files to the files dictionary
            foreach (var appFile in applicationFiles)
            {
                files[appFile.Key] = appFile.Value;
            }

            var rootContainer = files.ToRootContainer();

            var appImage = from rt in RuntimeFactory.Create(exec.Architecture)
                from rootCont in rootContainer
                from unixDir in Result.Try(() => rootCont.AsContainer().ToUnixDirectory())
                select new AppImageContainer(rt, unixDir);

            return appImage;
        });
    }

    private static Task<Result<INamedByteSource>> GetExecutable(IContainer applicationRoot)
    {
        return applicationRoot.Resources
            .Where(source => source.Name != "createdump" && !source.Name.EndsWith(".so"))
            .TryFirstResult(async source => await source.IsElf())
            .ToResult("No ELF executable found in the application root directory");
    }
}

public class Executable
{
    public INamedByteSource Resource { get; set; }
    public Architecture Architecture { get; set; }
}