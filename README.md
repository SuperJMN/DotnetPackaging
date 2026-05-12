# DotnetPackaging

Package your .NET app into the formats people actually install: AppImage, DEB, RPM, DMG, MSIX and Windows EXE installers, from one friendly .NET toolbox.

Use it however it fits your workflow:

- **CLI:** install `DotnetPackaging.Tool` and run `dotnetpackager` from your terminal or CI.
- **Libraries:** reference the NuGet packages directly and build packaging into your own tools.

The nice part: the packagers are pure .NET. You can create Linux packages, Windows installers and even macOS DMGs from Windows, macOS, Linux or a CI runner without collecting a different native toolchain for every format.

## Supported formats

| Output | Great for | Status |
|---|---|---|
| `.AppImage` | Portable Linux desktop apps | Supported |
| `.deb` | Debian, Ubuntu, Mint, Raspberry Pi OS | Supported |
| `.rpm` | Fedora, openSUSE, RHEL-like distros | Supported |
| `.dmg` | macOS drag-and-drop installers | Experimental |
| `.msix` | Windows app packages | Experimental |
| `.exe` | Windows self-extracting installers | Preview |

DEB and RPM can also package Linux services with `--service`, so backend apps are covered too.

## Install the CLI

```bash
dotnet tool install --global DotnetPackaging.Tool
```

## Package a project

Point `dotnetpackager` at a `.csproj` and it will publish and package the app in one go.

```bash
dotnetpackager appimage from-project \
  --project ./src/MyApp/MyApp.csproj \
  --arch x64 \
  --output ./artifacts/MyApp.AppImage \
  --application-name "My App" \
  --version 1.0.0
```

The same shape works for the other package types:

```bash
dotnetpackager deb from-project \
  --project ./src/MyApp/MyApp.csproj \
  --arch x64 \
  --output ./artifacts/myapp_1.0.0_amd64.deb \
  --application-name "My App" \
  --version 1.0.0

dotnetpackager rpm from-project \
  --project ./src/MyApp/MyApp.csproj \
  --arch x64 \
  --output ./artifacts/myapp-1.0.0-1.x86_64.rpm \
  --application-name "My App" \
  --version 1.0.0
```

And yes, DMGs are just as straightforward:

```bash
dotnetpackager dmg from-project \
  --project ./src/MyApp/MyApp.csproj \
  --arch arm64 \
  --output ./artifacts/MyApp.dmg \
  --application-name "My App" \
  --appId com.example.myapp \
  --version 1.0.0
```

Windows installers follow the same pattern:

```bash
dotnetpackager exe from-project \
  --project ./src/MyApp/MyApp.csproj \
  --arch x64 \
  --output ./artifacts/MyAppSetup.exe \
  --application-name "My App" \
  --appId com.example.myapp \
  --vendor "Example Co." \
  --version 1.0.0
```

## Package an existing publish folder

Already have `dotnet publish` output? Use `from-directory`.

```bash
dotnet publish ./src/MyApp/MyApp.csproj -c Release -r linux-x64 --self-contained -o ./publish/MyApp

dotnetpackager appimage from-directory \
  --directory ./publish/MyApp \
  --output ./artifacts/MyApp.AppImage \
  --application-name "My App" \
  --version 1.0.0
```

Swap `appimage` for `deb`, `rpm`, `dmg`, `msix` or `exe` when you want a different artifact.

## Use it as a library

The CLI is built on the same NuGet packages you can use in your own automation:

- `DotnetPackaging`
- `DotnetPackaging.AppImage`
- `DotnetPackaging.Deb`
- `DotnetPackaging.Rpm`
- `DotnetPackaging.Dmg`
- `DotnetPackaging.Msix`
- `DotnetPackaging.Exe`

Small example:

```csharp
using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Deb;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;

var publishDir = new DirectoryContainer(new FileSystem().DirectoryInfo.New("./publish/MyApp"));
var options = new FromDirectoryOptions()
    .WithName("My App")
    .WithPackage("myapp")
    .WithVersion("1.0.0")
    .WithSummary("A friendly .NET app");

var result = await new DebPackager()
    .Pack(publishDir.AsRoot(), options)
    .Bind(package => package.WriteTo("./artifacts/myapp_1.0.0_amd64.deb"));
```

That is the basic idea everywhere: give DotnetPackaging a published app plus metadata, get back a package you can save, upload or release.

## License

MIT. See [LICENSE](LICENSE).
