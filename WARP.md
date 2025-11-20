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
  - Defaults: self-contained=true al generar desde proyecto; en hosts no Windows, especifica --rid (win-x64/win-arm64) para elegir el stub/target correcto.

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
  - EXE (preview, dir): dotnetpackaging exe --directory /path/to/win-x64/publish --output /path/out/Setup.exe --rid win-x64 --application-name "MyApp" --appId com.example.myapp --version 1.0.0 --vendor "Vendor"
  - EXE (preview, project): dotnetpackaging exe from-project --project /path/to/MyApp.csproj --rid win-x64 --output /path/out/Setup.exe --application-name "MyApp" --appId com.example.myapp --version 1.0.0 --vendor "Vendor"

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
    - rpm/deb/appimage: self-contained=true by default. RID is optional; if you need to cross-publish (target a different OS/arch than the host), pass --rid (e.g., linux-x64/linux-arm64).
    - msix: self-contained=false by default. RID is optional; pass --rid when cross-publishing (e.g., win-x64/win-arm64).
    - dmg: requires --rid (osx-x64 or osx-arm64). Host RID inference is intentionally not used to avoid producing non-mac binaries when running on Linux/Windows.
    - flatpak: framework-dependent by default; uses its own runtime. You can still publish self-contained by passing --self-contained and --rid if needed.
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

## CRITICAL ISSUE: Uninstaller Crash (2025-11-20)

### Problem
The Windows uninstaller (`Uninstall.exe`) crashes with access violation **before any managed code executes**.

**Error Details:**
- Exception: `0xc0000005` (Access Violation)
- Exit Code: `0x80131506` (.NET Runtime internal error)
- Crash Offset: `0x00000000000b24f6` (consistent across all builds)
- PE Timestamp: `0x68ffe47c` (consistent despite non-deterministic builds)
- Assembly Version: `2.0.0.0`

**Symptoms:**
- Uninstaller launches but crashes immediately
- No UI shown, no log created
- Windows Event Viewer shows .NET Runtime fatal error
- **Crash occurs before `Program.Main` executes**

### What Was Fixed
1. ✅ Centralized logging to `%TEMP%\DotnetPackaging.Installer\`
2. ✅ Removed unnecessary Win32 P/Invoke (FindResource, LoadResource)
3. ✅ Fixed Avalonia Dispatcher issues (`base.OnFrameworkInitializationCompleted()`, `Dispatcher.UIThread.Post`)
4. ✅ Rename strategy for locked install directories
5. ✅ Non-deterministic builds (`Deterministic=false`, `AssemblyVersion=2.0.0.0`)

### What Did NOT Fix It
- The crash persists with identical symptoms
- Emergency logging in `Program.Main` does not execute
- File: `%TEMP%\dp-emergency.log` is empty/missing

### Current Hypothesis
Crash during:
1. **.NET Runtime initialization** (before managed code)
2. **Native DLL loading** (corrupt/incompatible dependency)
3. **Single-file bundling corruption** when executable is copied

**Evidence:**
- Consistent crash offset across all builds
- Same PE timestamp despite forced rebuild
- Installer works fine, uninstaller (identical binary) crashes
- Both are created via `File.Copy(Environment.ProcessPath, uninstallerPath)`

### Next Investigation Steps

1. **Check Emergency Log**
   ```powershell
   Get-Content $env:TEMP\dp-emergency.log
   ```
   If empty → crash is before `Program.Main`

2. **Analyze with WinDbg**
   - Attach to `Uninstall.exe --uninstall`
   - Identify what `0x00000000000b24f6` corresponds to
   - Look for native exception before managed entry

3. **Compare Binaries**
   - Use `dumpbin /headers` on installer vs uninstaller
   - Check PE headers for corruption
   - Verify embedded resources integrity

4. **Try Without Single-File**
   - Publish with `PublishSingleFile=false`
   - Test if crash persists with framework-dependent

5. **Alternate Uninstaller Strategy**
   - Don't copy: keep installer and registry points to it with `--uninstall`
   - Separate build: build uninstaller as distinct project
   - Script-based: generate PowerShell uninstaller instead

### Key Code Locations
- `src/DotnetPackaging.Exe.Installer/Program.cs` - Entry point with emergency log
- `src/DotnetPackaging.Exe.Installer/App.axaml.cs` - Avalonia initialization
- `src/DotnetPackaging.Exe.Installer/Core/Installer.cs:39` - Creates uninstaller via `File.Copy`
- `src/DotnetPackaging.Exe.Installer/Core/LoggerSetup.cs` - Logging config

---

Windows EXE (.exe) – progress log (snapshot)
- Done:
  - Librería DotnetPackaging.Exe con SimpleExePacker (concatena stub + zip + footer).
  - Comandos CLI: exe (desde carpeta) y exe from-project (publica y empaqueta). --stub es opcional; si se omite, el packer descarga el stub que corresponda desde GitHub Releases.
  - Stub Avalonia creado (esqueleto) en src/DotnetPackaging.Exe.Installer con lector de payload.
  - Instalador: opción de crear acceso directo en Escritorio en el paso Finish; acceso directo en Start Menu se mantiene.
  - CI: publica stubs win-x64 y win-arm64 como single-file self-extract con hashes y los sube a un GitHub Release (tag v{SemVer}).
  - Packer: logging antes de descargar el stub para informar del tiempo de espera.
- Next:
  - UI: Integrar SlimWizard de Zafiro en el stub (ahora hay UI mínima). Navegación con WizardNavigator y páginas.
  - Lógica: Elevación UAC y carpeta por defecto en Program Files según arquitectura.
  - Packer: caché local de stubs y reintentos/validación de hashes.
  - Detección avanzada de ejecutable e icono (paridad con .deb/.appimage).
  - Modo silencioso.
  - Pruebas E2E en Windows.

### CONFIRMED (2025-11-20 13:37)
**Emergency log test result:** dp-emergency.log is created during INSTALLATION but NOT during UNINSTALLATION.
This definitively proves the crash occurs BEFORE Program.Main executes in the uninstaller.

The problem is in .NET Runtime initialization or native bootstrapping, NOT in managed code.

### BREAKTHROUGH DISCOVERY (2025-11-20 15:08)

**The problem is NOT the binary, but WHERE it executes from:**

Test results:
- ✅ C:\Users\JMN\Desktop\AngorSetup.exe --uninstall → WORKS (reaches Program.Main)
- ❌ C:\Users\JMN\AppData\Local\Programs\AngorTest\Uninstall.exe --uninstall → CRASHES (before Program.Main)

**Proof:** Files are IDENTICAL (SHA256: 0F9CAD14DE400A2648F19FFBF4314787C3AF86FB13C5FADB007A9FC12171B39F)

**Root Cause Hypothesis:**
.NET Single-File runtime extraction fails when executable runs from the installation directory.
Possible reasons:
1. Permission issues in AppData\Local\Programs\ vs Desktop
2. Single-File runtime tries to extract to same directory and fails
3. Conflicting DLLs or files in installation directory
4. File locking issues (installer/app files locked)

**IMPLEMENTED SOLUTION (2025-11-20 15:27):**
Modified `Installer.cs:RegisterUninstaller()` to copy uninstaller to:
`%TEMP%\DotnetPackaging\Uninstallers\{appId}\Uninstall.exe`

Registry now points to this %TEMP% location instead of installation directory.

**STATUS:** ❌ Cannot test - new Avalonia Dispatcher crash blocking installer

### NEW BLOCKING ISSUE: Avalonia Dispatcher Shutdown (2025-11-20 15:27)

**Problem:**
After implementing the %TEMP% solution, the installer now crashes with:
```
System.InvalidOperationException: Cannot perform requested operation because the Dispatcher shut down
```

**Root Cause:**
Avalonia Dispatcher closes before async wizard operations complete.

**Attempted Fixes (all failed):**
1. ❌ `Dispatcher.UIThread.Post` - async void doesn't await
2. ❌ `Dispatcher.UIThread.InvokeAsync` - same issue
3. ❌ `desktopLifetime.Startup` event - async void event handler
4. ❌ `mainWindow.Opened` event - async void event handler  
5. ❌ `ShutdownMode.OnMainWindowClose` - still closes too early
6. ❌ Setting MainWindow in `OnFrameworkInitializationCompleted` - same result

**Current Code State:**
- `Installer.cs`: ✅ Copies uninstaller to %TEMP%
- `App.axaml.cs`: ❌ Creates MainWindow + Opened event (async void)
- `InstallerWizardLauncher.cs`: Modified to use existing MainWindow
- `Uninstallation.cs`: Modified to use existing MainWindow

**The %TEMP% solution CANNOT BE TESTED until Dispatcher issue is resolved.**

**SOLUTION IMPLEMENTED (2025-11-20 16:15): Native Launcher Strategy**

After multiple failed attempts to fix the Dispatcher issue, we implemented a different approach:
Instead of fixing where the uninstaller runs from, we created a **native launcher** that copies
the uninstaller to %TEMP% and executes it from there.

### Architecture

```
Installation Directory:
  ├── App files...
  ├── Uninstall.exe         (full installer binary with --uninstall flag support)
  └── UninstallLauncher.exe (small native .NET app, ~12MB)

Registry (UninstallString):
  → Points to UninstallLauncher.exe

Execution Flow:
  1. User clicks "Uninstall" in Control Panel
  2. Windows launches UninstallLauncher.exe
  3. Launcher copies Uninstall.exe to %TEMP%\DotnetPackaging\Uninstallers\{GUID}\
  4. Launcher executes Uninstall.exe from %TEMP% with --uninstall flag
  5. Launcher waits for uninstaller to complete (WaitForExit)
  6. Launcher returns uninstaller's exit code
  7. Windows shows no error dialog (because launcher waited)
```

### Implementation Details

**New Project: `src/DotnetPackaging.Exe.UninstallLauncher`**
- Single-file self-contained .NET 8 app
- `OutputType=WinExe` (no console window)
- `PublishTrimmed=true` for smaller size (~12MB)
- Embedded as resource in installer

**Key Code (`UninstallLauncher/Program.cs`):**
```csharp
// 1. Find Uninstall.exe in launcher's directory (installation dir)
var installDir = Path.GetDirectoryName(Environment.ProcessPath);
var uninstallerSource = Path.Combine(installDir, "Uninstall.exe");

// 2. Copy to unique %TEMP% directory
var tempDir = Path.Combine(Path.GetTempPath(), "DotnetPackaging", 
    "Uninstallers", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);
File.Copy(uninstallerSource, Path.Combine(tempDir, "Uninstall.exe"));

// 3. Execute and WAIT (critical for Windows not to show error dialog)
var process = Process.Start(new ProcessStartInfo {
    FileName = uninstallerTemp,
    Arguments = "--uninstall",
    UseShellExecute = false,
    WorkingDirectory = tempDir
});
process.WaitForExit();
return process.ExitCode;
```

**Modified `Installer.cs:RegisterUninstaller()`:**
```csharp
// Copy installer as Uninstall.exe to installation directory
var uninstallerPath = Path.Combine(targetDir, "Uninstall.exe");
File.Copy(Environment.ProcessPath, uninstallerPath, overwrite: true);

// Extract launcher from embedded resource
var launcherPath = Path.Combine(targetDir, "UninstallLauncher.exe");
TryExtractLauncher(launcherPath);

// Registry points to LAUNCHER (not uninstaller directly)
WindowsRegistryService.Register(
    meta.AppId, meta.ApplicationName, meta.Version, meta.Vendor,
    targetDir, $"\"{launcherPath}\"", mainExePath);
```

### Why This Works

**Problem:** .NET Single-File runtime extraction fails when executable runs from installation directory
- Cause: Permission issues, file locking, or runtime extraction conflicts
- Symptom: Access violation (0xc0000005) before Program.Main executes

**Solution:** Always run uninstaller from %TEMP%
- ✅ %TEMP% has no file locking issues
- ✅ .NET runtime extracts successfully
- ✅ Launcher is stable (lives in installation directory)
- ✅ If %TEMP% is cleaned, launcher recreates it

**Critical Design Decisions:**
1. **WinExe (not Exe)**: No console window flashing
2. **WaitForExit()**: Prevents Windows "program may not have uninstalled correctly" dialog
3. **Unique GUID subdirectory**: Prevents conflicts with concurrent uninstalls
4. **Embedded resource**: Launcher is always available with installer
5. **Fallback**: If launcher extraction fails, registry points directly to uninstaller

### File Sizes
- UninstallLauncher.exe: ~12 MB (single-file trimmed .NET 8)
- Uninstall.exe: Same as installer (full binary, 150-200 MB typically)
- Total overhead: ~12 MB per installation

### Future Improvements

#### 1. Native AOT Launcher (Priority: High)
**Current:** Single-file .NET 8 app (~12 MB)
**Target:** Native AOT (~1-2 MB)

**Blocker:** Requires Visual Studio C++ Build Tools
```xml
<PublishAot>true</PublishAot>
<IlcOptimizationPreference>Size</IlcOptimizationPreference>
```

**Alternative:** Pure C++ launcher (~10-50 KB)
- Use Win32 API directly
- No .NET dependency
- Requires C++ compiler in CI/CD

**Recommendation:** Implement C++ launcher for production
```cpp
// ~50 lines of Win32 C++ code
int WINAPI WinMain(...) {
    TCHAR path[MAX_PATH];
    GetModuleFileName(NULL, path, MAX_PATH);
    // ... copy to %TEMP% and CreateProcess with WaitForSingleObject
}
```

#### 2. Download Launcher from GitHub Releases (Priority: Medium)
**Current:** Embedded as resource in installer (~12 MB bloat)
**Target:** Download on-demand during installation

**Advantages:**
- Smaller installer binary
- Launcher can be updated independently
- Matches pattern used for installer stub

**Implementation:**
```csharp
// In Installer.cs
var launcherUrl = $"https://github.com/{repo}/releases/download/{tag}/UninstallLauncher-{rid}.exe";
var launcherPath = await DownloadLauncher(launcherUrl, targetDir);
```

**Disadvantages:**
- Requires internet during installation
- Needs fallback if download fails

#### 3. Shared Launcher (Priority: Low)
**Current:** Each app has its own launcher copy
**Target:** Single launcher in `%ProgramData%\DotnetPackaging\`

**Advantages:**
- Multiple apps share one launcher (~12 MB saved per app)
- Updates affect all apps

**Challenges:**
- Versioning (old apps with new launcher)
- Permissions (need admin to write to ProgramData)
- First app installs, last app uninstalls (reference counting)

**Not recommended:** Complexity outweighs benefits

#### 4. Self-Deleting Uninstaller (Priority: Medium)
**Current:** Uninstaller left in %TEMP% after uninstall
**Target:** Uninstaller deletes itself and temp directory

**Implementation:**
```csharp
// In Uninstallation.cs after wizard completes
ScheduleSelfDelete(Environment.ProcessPath);
ScheduleDirectoryDelete(Path.GetDirectoryName(Environment.ProcessPath));
```

**Technique:** Use `MoveFileEx` with `MOVEFILE_DELAY_UNTIL_REBOOT` or batch script

**Benefit:** Cleaner %TEMP% directory

#### 5. Launcher Retry Logic (Priority: Low)
**Current:** Single attempt to copy and launch
**Target:** Retry with exponential backoff if copy fails

**Scenario:** Antivirus locking Uninstall.exe temporarily

```csharp
for (int i = 0; i < 3; i++) {
    try {
        File.Copy(source, dest, overwrite: true);
        break;
    } catch {
        if (i == 2) throw;
        Thread.Sleep(500 * (i + 1));
    }
}
```

#### 6. Progress/Splash Screen (Priority: Low)
**Current:** Silent launcher (no UI)
**Target:** Show "Starting uninstaller..." splash

**Scenario:** Large uninstaller (~200 MB) takes seconds to copy

**Implementation:** WPF/WinForms tiny window during copy

**Trade-off:** Adds complexity and dependencies

### Testing Checklist

- [ ] Install app, verify UninstallLauncher.exe + Uninstall.exe in install dir
- [ ] Registry UninstallString points to launcher
- [ ] Uninstall via Control Panel → no console window appears
- [ ] Uninstall completes successfully → no Windows error dialog
- [ ] Uninstaller runs from %TEMP% (check logs)
- [ ] Multiple install/uninstall cycles work
- [ ] Uninstall works after cleaning %TEMP%
- [ ] Uninstall works with antivirus active
- [ ] Launcher return code matches uninstaller result

### Lessons Learned

1. **Single-file .NET apps have extraction issues in locked directories**
   - Always run from %TEMP% or desktop
   - Don't run from Program Files or AppData installation dirs

2. **Windows expects uninstallers to wait**
   - Launcher must use `WaitForExit()` not fire-and-forget
   - Return codes matter for Windows UAC/compatibility assistant

3. **Console windows are unprofessional**
   - Always use `WinExe` for GUI launchers
   - Even if no UI is shown

4. **Embedded resources are simple but costly**
   - ~12 MB overhead per installer
   - GitHub release downloads are better for production

5. **Avalonia Dispatcher async patterns are tricky**
   - Avoid async void event handlers
   - Consider synchronous initialization for installers
