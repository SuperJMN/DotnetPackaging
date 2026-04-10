# DotnetPackaging

DotnetPackaging helps you turn the publish output of a .NET application into ready-to-ship deliverables for Linux, Windows and macOS. The repository produces two related artifacts:

- **NuGet libraries** (`DotnetPackaging`, `DotnetPackaging.AppImage`, `DotnetPackaging.Deb`, `DotnetPackaging.Msix`, `DotnetPackaging.Dmg`, `DotnetPackaging.Exe`) that expose packaging primitives for tool authors and CI integrations.
- **A global `dotnet` tool** (`dotnetpackager`) that wraps those libraries with a scriptable command line experience.

Supported formats today: `.AppImage`, `.deb`, `.rpm`, `.msix` (experimental), `.dmg` (experimental) and a Windows self-extracting `.exe` (preview).

Both flavors share the same code paths, so whatever works in the CLI is also available from your own automation. The best part? Everything is pure .NET with zero native dependencies, so you can crank out packages from whatever OS you’re using without hunting for platform-specific toolchains.

## Why DotnetPackaging

Shipping .NET apps shouldn’t require juggling half a dozen platform tools. DotnetPackaging keeps things friendly by giving you one toolbox to generate installers and bundles for the ecosystems your users actually run. No extra daemons, no native SDK rabbit holes—just run the CLI or the libraries, and your bits are ready to share. It’s a laid-back, developer-first way to make sure your app lands everywhere it needs to.

## Repository layout
- `src/DotnetPackaging`: core abstractions such as metadata models, ELF inspection, icon discovery and option builders.
- `src/DotnetPackaging.AppImage`: AppImage-specific logic, including AppDir builders and runtime composition.
- `src/DotnetPackaging.Deb`: helpers to produce Debian control/data archives and emit `.deb` files.
- `src/DotnetPackaging.Tool`: the `dotnetpackager` CLI that consumes the libraries.
- `src/DotnetPackaging.DeployerTool` and `src/DotnetPackaging.Deployment`: optional utilities for publishing packages from CI setups.

All projects target .NET 10.

## Library usage

Every library works with the `Zafiro` filesystem abstractions so you can build packages from real directories or in-memory containers. The helpers infer reasonable defaults (architecture, executable, icon files, metadata) while still letting you override everything. Use the `*Packager` classes as the entry point; extension methods provide `FromProject` and `PackProject` conveniences.

### AppImage packages
Key capabilities:
- Build an AppImage straight from a published directory: no temporary copies, the directory is streamed into the SquashFS runtime.
- Generate intermediate AppDir structures if you want to tweak the contents before producing the final AppImage.
- Automatically detect the main executable (ELF inspection) and common icon files, with opt-in overrides.

```csharp
using DotnetPackaging.AppImage;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using Zafiro.FileSystem.Core;
using Zafiro.FileSystem.Local;

var publishDir = new Directory(new FileSystem().DirectoryInfo.New("./bin/Release/net10.0/linux-x64/publish"));
var appRoot = (await publishDir.ToDirectory()).Value;
var container = new DirectoryContainer(appRoot);

var metadata = new AppImagePackagerMetadata();
metadata.PackageOptions
    .WithId("com.example.myapp")
    .WithName("My App")
    .WithPackage("my-app")
    .WithVersion("1.0.0")
    .WithSummary("Cross-platform sample")
    .WithComment("Longer description shown in desktop menus");

var packager = new AppImagePackager();
var appImage = await packager.Pack(container.AsRoot(), metadata);
if (appImage.IsSuccess)
{
    await appImage.Value.WriteTo("./artifacts/MyApp.appimage");
}
```

For AppDir workflows, use the CLI subcommands (`appimage appdir` and `appimage from-appdir`).

### Debian packages
Key capabilities:
- Build `.deb` archives from any container or directory that resembles the install root of your app.
- Auto-detect the executable and architecture (with `FromDirectoryOptions` overrides when you know better).
- Emit `IByteSource` streams so you can persist packages to disk, upload them elsewhere, or plug them into other pipelines.
- **Install as a systemd service/daemon** with a single method call — generates the unit file, maintainer scripts, and automatic enable/start on install.

```csharp
using System.IO.Abstractions;
using DotnetPackaging.Deb;
using DotnetPackaging;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;

var publishDir = new DirectoryContainer(new FileSystem().DirectoryInfo.New("./bin/Release/net10.0/linux-x64/publish"));
var options = new FromDirectoryOptions()
    .WithName("My App")
    .WithPackage("my-app")
    .WithVersion("1.0.0")
    .WithSummary("Cross-platform sample app");

var packager = new DebPackager();
var debResult = await packager.Pack(publishDir.AsRoot(), options);

if (debResult.IsSuccess)
{
    await debResult.Value.WriteTo("./artifacts/MyApp_1.0.0_amd64.deb");
}
```

To install the application as a systemd service, call `WithService()`:

```csharp
var options = new FromDirectoryOptions()
    .WithName("My API")
    .WithPackage("my-api")
    .WithVersion("2.0.0")
    .WithSummary("Web API backend")
    .WithService(svc => svc
        .WithType(ServiceType.Notify)
        .WithRestart(RestartPolicy.Always)
        .WithUser("www-data")
        .WithEnvironment("DOTNET_ENVIRONMENT=Production", "ASPNETCORE_URLS=http://+:5000"));
```

The generated `.deb` will include a systemd unit file at `/lib/systemd/system/{package}.service` and maintainer scripts that `daemon-reload`, `enable`, and `start` the service on install, `stop` and `disable` on removal, and clean up on purge. This is the Linux equivalent of a Windows service — designed so .NET developers don't have to learn systemd internals.

`FromDirectoryOptions` exposes many more helpers (`WithExecutableName`, `WithIcon`, `WithHomepage`, `WithCategories`, `WithMaintainer`, etc.) so you can describe the package metadata you need.



## `dotnetpackager` CLI

The CLI is published as `DotnetPackaging.Tool` and installs a `dotnetpackager` command that mirrors the library APIs.

### Install
```bash
dotnet tool install --global DotnetPackaging.Tool
```

### Commands

Every format command offers two subcommands:
- **`from-directory`** – package from a pre-published directory (the output of `dotnet publish`).
- **`from-project`** – publish a .NET project and package in one step.

> **Deprecation notice:** invoking the base command directly with `--directory` (e.g. `dotnetpackager deb --directory`) still works for backward compatibility but is deprecated. Use `deb from-directory` instead. A future release will remove the deprecated form.

| Format | From directory | From project | Extra subcommands |
|---|---|---|---|
| **appimage** | `appimage from-directory` | `appimage from-project` | `appdir`, `from-appdir` |
| **deb** | `deb from-directory` | `deb from-project` | — |
| **rpm** | `rpm from-directory` | `rpm from-project` | — |
| **dmg** | `dmg from-directory` | `dmg from-project` | `verify` |
| **exe** | `exe from-directory` | `exe from-project` | — |
| **msix** | `msix from-directory` | `msix from-project` | — |

Run `dotnetpackager <command> --help` to see the full list of shared options (`--application-name`, `--comment`, `--homepage`, `--keywords`, `--icon`, `--is-terminal`, etc.).

### Examples
Build an AppImage from a published directory:
```bash
dotnetpackager appimage from-directory \
  --directory ./bin/Release/net10.0/linux-x64/publish \
  --output ./artifacts/MyApp.appimage \
  --application-name "My App" \
  --summary "Cross-platform sample" \
  --homepage https://example.com
```

Stage an AppDir and inspect it before packaging:
```bash
dotnetpackager appimage appdir \
  --directory ./bin/Release/net10.0/linux-x64/publish \
  --output-dir ./artifacts/MyApp.AppDir

# ...modify the AppDir contents if needed...

dotnetpackager appimage from-appdir \
  --directory ./artifacts/MyApp.AppDir \
  --output ./artifacts/MyApp.appimage
```

Produce a Debian package with a custom name and version:
```bash
dotnetpackager deb from-directory \
  --directory ./bin/Release/net10.0/linux-x64/publish \
  --output ./artifacts/MyApp_1.0.0_amd64.deb \
  --application-name "My App" \
  --summary "Cross-platform sample" \
  --comment "Longer description" \
  --homepage https://example.com \
  --license MIT \
  --version 1.0.0
```

Package a .NET project as a systemd service (publish + package in one step):
```bash
dotnetpackager deb from-project \
  --project ./src/MyApi/MyApi.csproj \
  --output ./artifacts/myapi.deb \
  --service
```

That single `--service` flag generates a systemd unit file, maintainer scripts for `daemon-reload`/`enable`/`start` on install and `stop`/`disable` on removal. Sensible defaults (`Type=simple`, `Restart=on-failure`) mean you rarely need anything else, but you can fine-tune:

```bash
dotnetpackager deb from-project \
  --project ./src/MyApi/MyApi.csproj \
  --output ./artifacts/myapi.deb \
  --service \
  --service-type notify \
  --service-restart always \
  --service-user www-data \
  --service-environment DOTNET_ENVIRONMENT=Production \
  --service-environment ASPNETCORE_URLS=http://+:5000
```

The `--service` flag also works from a pre-published directory:
```bash
dotnetpackager deb from-directory \
  --directory ./bin/Release/net10.0/linux-x64/publish \
  --output ./artifacts/myapi.deb \
  --application-name myapi \
  --service
```

| Service option | Default | Values |
|---|---|---|
| `--service` | *(off)* | Flag — enables systemd service mode |
| `--service-type` | `simple` | `simple`, `notify`, `forking`, `oneshot`, `idle` |
| `--service-restart` | `on-failure` | `no`, `always`, `on-failure`, `on-abnormal`, `on-abort`, `on-watchdog` |
| `--service-user` | *(none)* | Any Linux username |
| `--service-environment` | *(none)* | `KEY=VALUE` pairs (repeatable) |

All commands work on Windows, macOS or Linux, but the produced artifacts target Linux desktops (or Linux servers when using `--service`).

## Working on the repository
- Use the solution `DotnetPackaging.sln` and .NET SDK 10.0 or later.
- Unit tests live under `test/` (AppImage, Deb, Msix, etc.).
- `DotnetPackaging.DeployerTool` automates publishing NuGet packages and GitHub releases; see `azure-pipelines.yml` for a full CI example.

## License
The entire project is distributed under the MIT License. See `LICENSE` for details.

## Acknowledgements
- SquashFS support relies on [NyaFS](https://github.com/teplofizik/nyafs) by Alexey Sonkin.
- Filesystem abstractions come from the [Zafiro](https://github.com/SuperJMN/Zafiro) toolkit.
