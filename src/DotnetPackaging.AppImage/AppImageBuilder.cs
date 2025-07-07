using System.Reactive.Linq;
using System.Resources;
using DotnetPackaging.AppImage.Tests2;
using DotnetPackaging.AppImage.WIP;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;
using File = System.IO.File;

namespace DotnetPackaging.AppImage;

public class AppImageBuilder
{
    public async Task<Result<WIP.AppImage>> Create(IContainer applicationRoot, string appName)
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
            // Simulate the main executable
            var iconContent = ByteSource.FromString("fake-icon-bytes");
            var appRunContent = ByteSource.FromString($"#!/bin/bash\nexec \"$APPDIR/usr/bin/{appName}\" \"$@\"");
            var desktopContent = ByteSource.FromString($"[Desktop Entry]\nType=Application\nName={appName}\nExec={appName}\nIcon={appName}\n");
            var appdataContent = ByteSource.FromString("<component>\n  <id>myapp</id>\n  <name>MyApp</name>\n</component>");

            var files = new Dictionary<string, IByteSource>
            {
                ["AppRun"] = appRunContent,
                ["application.desktop"] = desktopContent,
                [".DirIcon"] = iconContent,
                [$"{appName}.png"] = iconContent,
                [$"usr/bin/{appName}"] = exec.Resource,
                [$"usr/share/icons/hicolor/64x64/apps/{appName}.png"] = iconContent,
                [$"usr/share/metainfo/{appName}.appdata.xml"] = appdataContent,
            }.ToRootContainer();

            var appImage = from rt in RuntimeFactory.Create(exec.Architecture)
                from rootContainer in files
                from unixDir in Result.Try(() => rootContainer.AsContainer().ToUnixDirectory())
                select new WIP.AppImage(rt, unixDir);

            return appImage;
        });
    }

    private Task<Result<INamedByteSource>> GetExecutable(IContainer applicationRoot)
    {
        // Assuming the application root contains a single ELF executable
        return applicationRoot.Resources
            .TryFirstResult(async source => await source.IsElf())
            .ToResult("No ELF executable found in the application root directory");
    }
}