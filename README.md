# DotnetPackaging

DotnetPackaging helps you turn the publish output of a .NET application into Linux-friendly deliverables. The repository produces two related artifacts:

- **NuGet libraries** (`DotnetPackaging`, `DotnetPackaging.AppImage`, `DotnetPackaging.Deb`) that expose the packaging primitives for tool authors and CI integrations.
- **A global `dotnet` tool** (`dotnetpackager`) that wraps those libraries with a scriptable command line experience.

Both flavors share the same code paths, so whatever works in the CLI is also available from your own automation.

## Repository layout
- `src/DotnetPackaging`: core abstractions such as metadata models, ELF inspection, icon discovery and option builders.
- `src/DotnetPackaging.AppImage`: AppImage-specific logic, including AppDir builders and runtime composition.
- `src/DotnetPackaging.Deb`: helpers to produce Debian control/data archives and emit `.deb` files.
- `src/DotnetPackaging.Tool`: the `dotnetpackager` CLI that consumes the libraries.
- `src/DotnetPackaging.DeployerTool` and `src/DotnetPackaging.Deployment`: optional utilities for publishing packages from CI setups.

All projects target .NET 8.

## Library usage

Every library works with the `Zafiro` filesystem abstractions so you can build packages from real directories or in-memory containers. The helpers infer reasonable defaults (architecture, executable, icon files, metadata) while still letting you override everything.

### AppImage packages
Key capabilities:
- Build an AppImage straight from a published directory: no temporary copies, the directory is streamed into the SquashFS runtime.
- Generate intermediate AppDir structures if you want to tweak the contents before producing the final AppImage.
- Automatically detect the main executable (ELF inspection) and common icon files, with opt-in overrides.

```csharp
using DotnetPackaging.AppImage;
using DotnetPackaging.AppImage.Metadata;
using Zafiro.DivineBytes;
using Zafiro.FileSystem.Core;
using Zafiro.FileSystem.Local;

var publishDir = new Directory(new FileSystem().DirectoryInfo.New("./bin/Release/net8.0/linux-x64/publish"));
var appRoot = (await publishDir.ToDirectory()).Value;
var container = new DirectoryContainer(appRoot);

var metadata = new AppImageMetadata("com.example.myapp", "My App", "my-app")
{
    Version = "1.0.0",
    Summary = "Cross-platform sample",
    Comment = "Longer description shown in desktop menus",
};

var factory = new AppImageFactory();
var appImage = await factory.Create(container.AsRoot(), metadata);
if (appImage.IsSuccess)
{
    await appImage.Value.ToByteSource()
        .Bind(bytes => bytes.WriteTo("./artifacts/MyApp.appimage"));
}
```

You can also call `factory.BuildAppDir(...)` to materialise an AppDir on disk, or `factory.CreateFromAppDir(...)` when you already have an AppDir layout.

### Debian packages
Key capabilities:
- Build `.deb` archives from any container or directory that resembles the install root of your app.
- Auto-detect the executable and architecture (with `FromDirectoryOptions` overrides when you know better).
- Emit `IData` streams so you can persist packages to disk, upload them elsewhere, or plug them into other pipelines.

```csharp
using System.IO.Abstractions;
using DotnetPackaging.Deb;
using DotnetPackaging;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;

var publishDir = new DirectoryContainer(new FileSystem().DirectoryInfo.New("./bin/Release/net8.0/linux-x64/publish"));
var debResult = await DebFile.From()
    .Container(publishDir.AsRoot(), publishDir.Name)
    .Configure(options =>
    {
        options.WithName("My App")
               .WithPackage("my-app")
               .WithVersion("1.0.0")
               .WithSummary("Cross-platform sample app");
    })
    .Build();

if (debResult.IsSuccess)
{
    await debResult.Value.ToData().DumpTo("./artifacts/MyApp_1.0.0_amd64.deb");
}
```

`FromDirectoryOptions` exposes many more helpers (`WithExecutableName`, `WithIcon`, `WithHomepage`, `WithCategories`, `WithMaintainer`, etc.) so you can describe the package metadata you need.

## `dotnetpackager` CLI

The CLI is published as `DotnetPackaging.Tool` and installs a `dotnetpackager` command that mirrors the library APIs.

### Install
```bash
dotnet tool install --global DotnetPackaging.Tool
```

### Commands
- `dotnetpackager deb` – build a Debian/Ubuntu `.deb` out of a publish directory.
- `dotnetpackager appimage` – build an `.AppImage` file directly from a publish directory.
- `dotnetpackager appimage appdir` – generate an AppDir folder structure for inspection/customisation.
- `dotnetpackager appimage from-appdir` – package an existing AppDir into an AppImage.

Run `dotnetpackager <command> --help` to see the full list of shared options (`--application-name`, `--comment`, `--homepage`, `--keywords`, `--icon`, `--is-terminal`, etc.).

### Examples
Build an AppImage in one go:
```bash
dotnetpackager appimage \
  --directory ./bin/Release/net8.0/linux-x64/publish \
  --output ./artifacts/MyApp.appimage \
  --application-name "My App" \
  --summary "Cross-platform sample" \
  --homepage https://example.com
```

Stage an AppDir and inspect it before packaging:
```bash
dotnetpackager appimage appdir \
  --directory ./bin/Release/net8.0/linux-x64/publish \
  --output-dir ./artifacts/MyApp.AppDir

# ...modify the AppDir contents if needed...

dotnetpackager appimage from-appdir \
  --directory ./artifacts/MyApp.AppDir \
  --output ./artifacts/MyApp.appimage
```

Produce a Debian package with a custom name and version:
```bash
dotnetpackager deb \
  --directory ./bin/Release/net8.0/linux-x64/publish \
  --output ./artifacts/MyApp_1.0.0_amd64.deb \
  --application-name "My App" \
  --summary "Cross-platform sample" \
  --comment "Longer description" \
  --homepage https://example.com \
  --license MIT \
  --version 1.0.0
```

All commands work on Windows, macOS or Linux, but the produced artifacts target Linux desktops.

## Working on the repository
- Use the solution `DotnetPackaging.sln` and .NET SDK 8.0 or later.
- Unit tests live under `test/` (AppImage, Deb, Msix, etc.).
- `DotnetPackaging.DeployerTool` automates publishing NuGet packages and GitHub releases; see `azure-pipelines.yml` for a full CI example.

## License
The entire project is distributed under the MIT License. See `LICENSE` for details.

## Acknowledgements
- SquashFS support relies on [NyaFS](https://github.com/teplofizik/nyafs) by Alexey Sonkin.
- Filesystem abstractions come from the [Zafiro](https://github.com/SuperJMN/Zafiro) toolkit.
