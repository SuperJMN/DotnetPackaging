using System.Reactive.Linq;
using DotnetPackaging.AppImage.Core;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage;

public class AppImageFactory
{
    public async Task<Result<AppImageContainer>> Create(IContainer applicationRoot, string appName)
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
            var appRunContent = ByteSource.FromString($"#!/bin/bash\nexec \"$APPDIR/usr/bin/{appName}\" \"$@\"");
            var desktopContent = ByteSource.FromString($"[Desktop Entry]\nType=Application\nName={appName}\nExec={appName}\nIcon={appName}\n");
            var appdataContent = ByteSource.FromString("<component>\n  <id>myapp</id>\n  <name>MyApp</name>\n</component>");

            var files = new Dictionary<string, IByteSource>
            {
                ["AppRun"] = appRunContent,
                ["application.desktop"] = desktopContent,
                [$"usr/bin/{appName}"] = exec.Resource,
                [$"usr/share/metainfo/{appName}.appdata.xml"] = appdataContent,
            }.ToRootContainer();

            var appImage = from rt in RuntimeFactory.Create(exec.Architecture)
                from rootContainer in files
                from unixDir in Result.Try(() => rootContainer.AsContainer().ToUnixDirectory())
                select new AppImageContainer(rt, unixDir);

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