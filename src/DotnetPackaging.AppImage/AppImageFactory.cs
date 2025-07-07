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
        var executable = from exec in GetExecutable(applicationRoot)
            from arch in exec.GetArchitecture()
            select new
            {
                Resource = exec,
                Architecture = arch,
            };

        return await executable.Bind(exec =>
        {
            var executableName = appImageMetadata.PackageName;
            
            var desktopFile = appImageMetadata.ToDesktopFile($"/usr/bin/{executableName}");

            var appRunContent = ByteSource.FromString($"#!/bin/bash\nexec \"$APPDIR/usr/bin/{executableName}\" \"$@\"");
            var desktopContent = ByteSource.FromString(MetadataGenerator.DesktopFileContents(desktopFile));
            var appdataContent = ByteSource.FromString(MetadataGenerator.AppStreamXml(appImageMetadata.ToAppStream()));

            var files = new Dictionary<string, IByteSource>
            {
                ["AppRun"] = appRunContent,
                [appImageMetadata.DesktopFileName] = desktopContent,
                [$"usr/bin/{executableName}"] = exec.Resource,
                [$"usr/share/metainfo/{appImageMetadata.AppDataFileName}"] = appdataContent,
            }.ToRootContainer();

            var appImage = from rt in RuntimeFactory.Create(exec.Architecture)
                from rootContainer in files
                from unixDir in Result.Try(() => rootContainer.AsContainer().ToUnixDirectory())
                select new AppImageContainer(rt, unixDir);

            return appImage;
        });
    }

    private static Task<Result<INamedByteSource>> GetExecutable(IContainer applicationRoot)
    {
        return applicationRoot.Resources
            .TryFirstResult(async source => await source.IsElf())
            .ToResult("No ELF executable found in the application root directory");
    }
}