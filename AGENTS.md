# Guía de estilo y operaciones generales

Guía operativa y de estilo para trabajar con este repositorio usando agentes.

Precedencia de reglas
- Las reglas se aplican en orden de precedencia creciente: las que aparecen más tarde prevalecen sobre las anteriores.
- Las reglas de proyecto (asociadas a rutas concretas) tienen prioridad sobre reglas personales.
- Entre reglas de proyecto, las de subdirectorios prevalecen sobre las del directorio padre.

## Comunicación y formato
- Conversaciones y asistencia: en español.
- Código, mensajes de commit, comentarios de código y resúmenes de PR: en inglés.
- PR: usar texto sin escapar en asunto y cuerpo.

## Terminal y ejecución
- No cerrar la terminal ni ejecutar comandos que finalicen la sesión.
- Evitar comandos interactivos salvo que sea estrictamente necesario.
- Extremar cuidado con comillas simples y dobles en los comandos.

## Despliegue y CI

- Se realiza mediante azure-pipelines.yml.
- La build debe pasar correctamente antes de fusionar una PR.

## Lineamientos de diseño y estilo (C# / Reactive)

- Preferir programación funcional y reactiva cuando no complique en exceso.
- Validación: preferir ReactiveUI.Validations.
- Result handling: usar CSharpFunctionalExtensions cuando sea posible.
- Convenciones:
  - No usar sufijo “Async” en métodos que devuelven Task.
  - No usar guiones bajos para campos privados.
  - Evitar eventos (salvo indicación explícita).
  - Favorecer inmutabilidad; mutar solo lo estrictamente necesario.
  - Evitar poner lógica en Observable.Subscribe; preferir encadenar operadores y proyecciones.

# Errores y notificaciones

- Para flujos de Result<T> usar el operador Successes.
- Para fallos, HandleErrorsWith() empleando INotificationService para notificar al usuario.

# Toolkit Zafiro

Es mi propio toolkit. Disponible en https://github.com/SuperJMN/Zafiro. Muchos de los métodos que no conozcas pueden formar parte de este toolkit. Tenlo en consideración.

# Manejo de bytes (sin Streams imperativos)

- Usar Zafiro.DivineBytes para flujos de bytes evitables con Stream.
- ByteSource es la abstracción observable y componible equivalente a un stream de lectura.

# Refactorización guiada por responsabilidades

1. Leer el código y describir primero sus responsabilidades.
2. Enumerar cada responsabilidad como una frase nominal clara.
3. Para cada responsabilidad, crear una clase o método con nombre específico y semántico.
4. Extraer campos y dependencias según cada responsabilidad.
5. Evitar variables compartidas entre responsabilidades; si aparecen, replantear los límites.
6. No introducir patrones arbitrarios; mantener la interfaz pública estable.
7. No eliminar logs ni validaciones existentes.


# General guidelines about this repo

This file guides Warp (and future contributors) on how CI/CD and packaging work in this repository.

Scope: whole repository (DotnetPackaging).

CI pipeline (Azure Pipelines)
- Definition: azure-pipelines.yml at repo root.
- Agent: windows-latest.
- Versioning: computed with GitVersion.Tool; packages use MajorMinorPatch as Version; GitHub Release tag uses v{SemVer}.
- Behavior on master:
  - Restore, build and pack all projects; push .nupkg (non-symbol) to NuGet (skip-duplicate) with $(NuGetApiKey).
  - Publish Windows EXE stubs (DotnetPackaging.Exe.Installer) for win-x64 and win-arm64 as single-file self-extract apps (IncludeNativeLibrariesForSelfExtract/IncludeAllContentForSelfExtract, no trimming).
  - Produce .sha256 for each stub and upload both .exe and .sha256 to a GitHub Release tagged v{SemVer} using gh CLI.
- Other branches/PRs: build and pack only (no push, no release).
- Packable projects: every project with IsPackable/PackAsTool set. The CLI tool lives in src/DotnetPackaging.Tool (PackAsTool=true).

Versioning (GitVersion)
- GitVersion.Tool runs in CI to produce:
  - Version: MajorMinorPatch (used for dotnet build/pack).
  - TagName: v{SemVer} (used to create/update the GitHub Release).
- Practical effect: merging to master triggers package publish to NuGet and stub upload to a GitHub Release for the computed tag.

Secrets
- The pipeline expects a variable group named api-keys providing:
  - NuGetApiKey: API key used to push packages to NuGet.
  - GitHubApiKey: token exposed as GITHUB_TOKEN to create/update releases and upload stub assets via gh.
- Do not hardcode secrets. Locally, export environment variables and pass them to the CLI tools.

Local replication
- Pack locally:
  - dotnet restore
  - dotnet build -c Release -p:ContinuousIntegrationBuild=true -p:Version=1.2.3 --no-restore
  - dotnet pack -c Release --no-build -p:IncludeSymbols=false -p:SymbolPackageFormat=snupkg -p:Version=1.2.3 -o ./artifacts/nuget
- Push to NuGet:
  - For each .nupkg (non-symbol): dotnet nuget push ./artifacts/nuget/<pkg>.nupkg --api-key "$env:NUGET_API_KEY" --source https://api.nuget.org/v3/index.json --skip-duplicate
- Build Windows stubs (on Windows):
  - dotnet publish src/DotnetPackaging.Exe.Installer/DotnetPackaging.Exe.Installer.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:PublishTrimmed=false -o ./artifacts/stubs/win-x64
  - Repeat for win-arm64 by changing -r.
- Release (optional):
  - gh release create v1.2.3 --title "DotnetPackaging 1.2.3" --notes "Local release"
  - gh release upload v1.2.3 ./artifacts/stubs/win-*/DotnetPackaging.Exe.Installer*.exe ./artifacts/stubs/win-*/DotnetPackaging.Exe.Installer*.exe.sha256 -R <owner>/<repo>

Notes
- Because the CLI is a dotnet tool (PackAsTool=true) and is included in the solution, CI will pack and publish it to NuGet alongside the libraries when running on master.
- The pipeline performs a shallow fetch depth override (full history) to ensure GitVersion/describe work correctly.

Packaging formats: status and details
- AppImage (Linux)
  - Status: supported and used. Library: src/DotnetPackaging.AppImage (net8.0).
  - How it works: builds an AppDir from a published app directory, generates AppRun, .desktop and AppStream files, discovers icons, then creates a SquashFS and concatenates the official AppImage runtime.
  - Runtime retrieval: downloads AppImageKit runtime per architecture (x86_64/armhf/aarch64) via RuntimeFactory. No external tools required (no linuxdeploy, no appimagetool).
  - Icon strategy: automatic discovery under the provided directory; if none, falls back to common names (icon.svg, icon-256.png, icon.png). Optionally writes .DirIcon.
  - Debugging: set DOTNETPACKAGING_DEBUG=1 to dump intermediate Runtime*.runtime and Image*-Container.sqfs in the temp folder.
- Debian .deb (Linux)
  - Status: supported and used. Library: src/DotnetPackaging.Deb.
  - How it works: lays out files under /opt/<package>, generates .desktop under /usr/local/share/applications and a wrapper under /usr/local/bin/<package>.
  - Packaging: fully managed (no external dpkg-deb required). Icons are optionally embedded under hicolor/<size>/apps/<package>.png.
- RPM .rpm (Linux)
  - Status: supported and used. Library: src/DotnetPackaging.Rpm; exposed via CLI.
  - How it works: stages files under /opt/<package>, writes a .desktop in /usr/share/applications and a wrapper in /usr/bin/<package>.
  - Packaging: uses system rpmbuild to assemble the package. Requires rpm-build to be installed locally.
  - Portability: auto-generated dependency/provides under /opt/<package> are excluded (using %global __requires_exclude_from/__provides_exclude_from), preventing hard Requires like liblttng-ust.so.0 and making self-contained .NET apps install across RPM-based distros.
  - Ownership: the package only owns directories under /opt/<package>; system directories (/usr, /usr/bin, etc.) are not owned to avoid conflicts with filesystem packages.
- MSIX (Windows)
  - Status: experimental/preview. Library: src/DotnetPackaging.Msix with tests in src/DotnetPackaging.Msix.Tests.
  - CLI: exposed via msix pack (from directory) and msix from-project (publishes then packs). Assumes a valid AppxManifest.xml and assets are present; metadata generation can be added.
  - Validation: tests unpack resulting MSIX using makeappx tooling in CI-like conditions.
- Flatpak (Linux)
  - Status: supported (bundle via system `flatpak`) with internal OSTree bundler fallback.
  - Libraries: src/DotnetPackaging.Flatpak (Factory, Packer, OSTree scaffolding).
  - How it works: builds a Flatpak layout (metadata at root, files/ subtree) from a publish directory; icons auto-detected and installed under files/share/icons/.../apps/<appId>.(svg/png). Desktop Icon is forced to <appId>.
  - Bundling: prefers system `flatpak build-export/build-bundle`; if not available or fails, uses internal bundler to emit a single-file `.flatpak` (unsigned, for testing).
  - Defaults: freedesktop runtime 24.08 (runtime/sdk), branch=stable, common permissions (network/ipc, wayland/x11/pulseaudio, dri, filesystem=home). Command defaults to AppId.
- DMG .dmg (macOS)
  - Status: experimental cross-platform builder. Library: src/DotnetPackaging.Dmg.
  - How it works: emits an ISO9660/Joliet image (UDTO) with optional .app scaffolding if none exists. Special adornments like .VolumeIcon.icns and .background are hoisted to the image root when present.
  - Notes: intended for simple drag-and-drop installs. Not a full UDIF/UDZO implementation; signing and advanced Finder layouts are out of scope for now.
- Windows EXE (.exe) — preview
  - Status: preview. Dotnet-only SFX builder. Library: src/DotnetPackaging.Exe. Stub Avalonia: src/DotnetPackaging.Exe.Installer (esqueleto WIP).
  - How it works: produces a self-extracting installer by concatenating [stub.exe][payload.zip][Int64 length]["DPACKEXE1"]. The payload contains metadata.json and Content/ (publish output). The stub leerá metadata y realizará la instalación.
  - CLI: exe (desde carpeta publish) y exe from-project (publica y empaqueta). Si omites --stub, el packer descargará automáticamente el stub que corresponda desde GitHub Releases; puedes pasar --stub para forzar uno concreto.
  - Cross-platform build: el empaquetado (concatenación) funciona desde cualquier SO. El stub se publica por RID (win-x64/win-arm64).
  - Defaults: self-contained=true al generar desde proyecto; en hosts no Windows, especifica --arch (x64/arm64) para elegir el stub/target correcto.

CLI tool (dotnet tool)
- Project: src/DotnetPackaging.Tool (PackAsTool=true, ToolCommandName=dotnetpackaging).
- Commands available:
  - appimage: create an AppImage from a directory (typically dotnet publish output). Autodetects executable + architecture; generates metadata and icons.
  - appimage from-project: publish a .NET project and build an AppImage.
  - deb: create a .deb from a directory (dotnet publish output). Detects executable; generates metadata, .desktop and wrapper.
  - deb from-project: publish a .NET project and build a .deb.
  - rpm: create an .rpm from a directory (dotnet publish output). Uses rpmbuild; excludes auto-deps under /opt/<package> and avoids owning system dirs.
  - rpm from-project: publish a .NET project and build an .rpm.
  - flatpak: layout, bundle (system or internal), repo, and pack (minimal UX).
  - flatpak from-project: publish a .NET project and build a .flatpak bundle.
  - msix (experimental): msix pack (from directory) and msix from-project.
  - dmg (experimental): dmg (from directory) and dmg from-project (publishes then builds a .dmg).
  - exe (preview): Windows self-extracting installer (.exe) from directory; and exe from-project (publica y empaqueta). Si omites --stub, se descargará el stub apropiado automáticamente.
- Common options (all commands share a metadata set):
  - --directory <dir> (required): input directory to package from.
  - --output <file> (required): output file (.AppImage, .deb, .rpm, .msix, .flatpak, .dmg).
  - --application-name, --wm-class, --main-category, --additional-categories, --keywords, --comment, --version,
    --homepage, --license, --screenshot-urls, --summary, --appId, --executable-name, --is-terminal, --icon <path>.
- Examples (from a published folder):
  - AppImage (dir): dotnetpackaging appimage --directory /path/to/publish --output /path/out/MyApp.AppImage --application-name "MyApp"
  - AppImage (project): dotnetpackaging appimage from-project --project /path/to/MyApp.csproj --output /path/out/MyApp.AppImage --application-name "MyApp"
  - Deb (dir):      dotnetpackaging deb      --directory /path/to/publish --output /path/out/myapp_1.0.0_amd64.deb --application-name "MyApp"
  - Deb (project):  dotnetpackaging deb from-project --project /path/to/MyApp.csproj --output /path/out/myapp_1.0.0_amd64.deb --application-name "MyApp"
  - RPM (dir):      dotnetpackaging rpm      --directory /path/to/publish --output /path/out/myapp-1.0.0-1.x86_64.rpm --application-name "MyApp"
  - RPM (project):  dotnetpackaging rpm from-project --project /path/to/MyApp.csproj --output /path/out/myapp-1.0.0-1.x86_64.rpm --application-name "MyApp"
  - Flatpak (minimal): dotnetpackaging flatpak pack --directory /path/to/publish --output-dir /path/out
  - Flatpak (bundle):  dotnetpackaging flatpak bundle --directory /path/to/publish --output /path/out/MyApp.flatpak --system
  - Flatpak (project): dotnetpackaging flatpak from-project --project /path/to/MyApp.csproj --output /path/out/MyApp.flatpak --system
  - MSIX (dir, experimental): dotnetpackaging msix pack --directory /path/to/publish --output /path/out/MyApp.msix
  - MSIX (project, experimental): dotnetpackaging msix from-project --project /path/to/MyApp.csproj --output /path/out/MyApp.msix
  - DMG (dir, experimental): dotnetpackaging dmg --directory /path/to/publish --output /path/out/MyApp.dmg --application-name "MyApp"
  - DMG (project, experimental): dotnetpackaging dmg from-project --project /path/to/MyApp.csproj --output /path/out/MyApp.dmg --application-name "MyApp"
  - EXE (preview, dir): dotnetpackaging exe --directory /path/to/win-x64/publish --output /path/out/Setup.exe --arch x64 --application-name "MyApp" --appId com.example.myapp --version 1.0.0 --vendor "Vendor"
  - EXE (preview, project): dotnetpackaging exe from-project --project /path/to/MyApp.csproj --arch x64 --output /path/out/Setup.exe --application-name "MyApp" --appId com.example.myapp --version 1.0.0 --vendor "Vendor"

Tests
- AppImage tests (test/DotnetPackaging.AppImage.Tests):
  - CreateAppImage validates building from containers and saving bytes.
  - SquashFS tests ensure filesystem construction integrity.
- Deb tests (test/DotnetPackaging.Deb.Tests):
  - Integration tests covering metadata and tar entries layout.
- MSIX tests (src/DotnetPackaging.Msix.Tests):
  - Validate building MSIX and unpacking with makeappx to assert structure.
- EXE tests (test/DotnetPackaging.Exe.Tests):
  - Validate metadata zip creation and concatenation format; basic install path resolution.
- Gaps / TODOs:
  - Add CLI end-to-end tests (invocation of dotnetpackaging appimage/deb/rpm/exe on temp publishes and validating outputs).
  - Integrate dotnet test into azure-pipelines.yml.
  - Improve EXE installer UI and add Windows E2E tests.

Developer workflow tips
- Publish input
  - AppImage/Deb/RPM/Flatpak/MSIX consume a folder produced by dotnet publish. from-project subcommands invoke a minimal publisher and reuse the same pipelines.
  - For AppImage, ensure an ELF executable is present (self-contained single-file publish is acceptable). If not specified, the first eligible ELF is chosen.
- RID/self-contained
  - from-project defaults:
    - rpm/deb/appimage: self-contained=true by default. Architecture is optional; if necesitas cross-publish, pasa --arch (x64/arm64) y se mapeara al RID correspondiente.
    - msix: self-contained=false by default. Architecture is optional; pasa --arch cuando cross-publishing (x64/arm64).
    - dmg: requires --arch (x64 o arm64). Host RID inference is intentionally not used to avoid producing non-mac binaries when running on Linux/Windows.
    - flatpak: framework-dependent by default; uses its own runtime. You can still publish self-contained by passing --self-contained and --arch if needed.
- RPM prerequisites
  - Install rpmbuild tooling: dnf install -y rpm-build (or the equivalent on your distro).
  - The RPM builder excludes auto-deps/provides under /opt/<package> to keep self-contained .NET apps portable and avoid liblttng-ust.so.N issues across distros.
  - The package does not own system directories; only /opt/<package> and files explicitly installed (wrapper under /usr/bin and .desktop in /usr/share/applications).
- Icon handling
  - The CLI and libraries attempt to discover icons automatically. You can override via --icon or supply common names in the root (icon.svg, icon-256.png, icon.png).
- Debug
  - Set DOTNETPACKAGING_DEBUG=1 to dump AppImage intermediate artifacts (runtime + squashfs).

Repository map (relevant)
- src/DotnetPackaging.AppImage: AppImage core (AppImageFactory, RuntimeFactory, SquashFS).
- src/DotnetPackaging.Deb: Debian packaging (Tar entries, DebFile).
- src/DotnetPackaging.Rpm: RPM packaging (layout builder and rpmbuild spec generation).
- src/DotnetPackaging.Msix: MSIX packaging (builder and helpers).
- src/DotnetPackaging.Exe: Windows SFX packer (concatenation and metadata).
- src/DotnetPackaging.Exe.Installer: Avalonia stub installer.
- src/DotnetPackaging.Tool: CLI (dotnet tool) with commands appimage, deb, rpm, flatpak, msix, exe.
- test/*: AppImage and Deb tests; src/DotnetPackaging.Msix.Tests for MSIX; test/DotnetPackaging.Exe.Tests for EXE packaging.

Backlog / Future work
- Add CLI E2E tests (including rpm/exe) and hook dotnet test in CI.
- Optional: enrich icon detection strategies and metadata mapping (e.g., auto-appId from name + reverse DNS).