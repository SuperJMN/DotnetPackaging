# GuÌa de estilo y operaciones generales

GuÌa operativa y de estilo para trabajar con este repositorio usando agentes.

Precedencia de reglas
- Las reglas se aplican en orden de precedencia creciente: las que aparecen m·s tarde prevalecen sobre las anteriores.
- Las reglas de proyecto (asociadas a rutas concretas) tienen prioridad sobre reglas personales.
- Entre reglas de proyecto, las de subdirectorios prevalecen sobre las del directorio padre.

## ComunicaciÛn y formato
- Conversaciones y asistencia: en espaÒol.
- CÛdigo, mensajes de commit, comentarios de cÛdigo y res˙menes de PR: en inglÈs.
- PR: usar texto sin escapar en asunto y cuerpo.

## Terminal y ejecuciÛn
- No cerrar la terminal ni ejecutar comandos que finalicen la sesiÛn.
- Evitar comandos interactivos salvo que sea estrictamente necesario.
- Extremar cuidado con comillas simples y dobles en los comandos.

## Despliegue y CI

- Se realiza mediante azure-pipelines.yml.
- La build debe pasar correctamente antes de fusionar una PR.

## Lineamientos de diseÒo y estilo (C# / Reactive)

- Preferir programaciÛn funcional y reactiva cuando no complique en exceso.
- ValidaciÛn: preferir ReactiveUI.Validations.
- Result handling: usar CSharpFunctionalExtensions cuando sea posible.
- Convenciones:
    - No usar sufijo ìAsyncî en mÈtodos que devuelven Task.
    - No usar guiones bajos para campos privados.
    - Evitar eventos (salvo indicaciÛn explÌcita).
    - Favorecer inmutabilidad; mutar solo lo estrictamente necesario.
    - Evitar poner lÛgica en Observable.Subscribe; preferir encadenar operadores y proyecciones.

# Errores y notificaciones

- Para flujos de Result<T> usar el operador Successes.
- Para fallos, HandleErrorsWith() empleando INotificationService para notificar al usuario.

# Toolkit Zafiro

Es mi propio toolkit. Disponible en https://github.com/SuperJMN/Zafiro. Muchos de los mÈtodos que no conozcas pueden formar parte de este toolkit. Tenlo en consideraciÛn.

# Manejo de bytes (sin Streams imperativos)

- Usar Zafiro.DivineBytes para flujos de bytes evitables con Stream.
- ByteSource es la abstracciÛn observable y componible equivalente a un stream de lectura.

# RefactorizaciÛn guiada por responsabilidades

1. Leer el cÛdigo y describir primero sus responsabilidades.
2. Enumerar cada responsabilidad como una frase nominal clara.
3. Para cada responsabilidad, crear una clase o mÈtodo con nombre especÌfico y sem·ntico.
4. Extraer campos y dependencias seg˙n cada responsabilidad.
5. Evitar variables compartidas entre responsabilidades; si aparecen, replantear los lÌmites.
6. No introducir patrones arbitrarios; mantener la interfaz p˙blica estable.
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
- Windows EXE (.exe) ó preview
  - Status: preview. Dotnet-only SFX builder. Library: src/DotnetPackaging.Exe. Stub Avalonia: src/DotnetPackaging.Exe.Installer (esqueleto WIP).
  - How it works: produces a self-extracting installer by concatenating [stub.exe][payload.zip][Int64 length]["DPACKEXE1"]. The payload contains metadata.json and Content/ (publish output). The stub leer· metadata y realizar· la instalaciÛn.
  - CLI: exe (desde carpeta publish) y exe from-project (publica y empaqueta). Si omites --stub, el packer descargar· autom·ticamente el stub que corresponda desde GitHub Releases; puedes pasar --stub para forzar uno concreto.
  - Cross-platform build: el empaquetado (concatenaciÛn) funciona desde cualquier SO. El stub se publica por RID (win-x64/win-arm64).
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
  - exe (preview): Windows self-extracting installer (.exe) from directory; and exe from-project (publica y empaqueta). Si omites --stub, se descargar· el stub apropiado autom·ticamente.
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

Windows EXE (.exe) ñ progress log (snapshot)
- Done:
  - LibrerÌa DotnetPackaging.Exe con SimpleExePacker (concatena stub + zip + footer).
  - Comandos CLI: exe (desde carpeta) y exe from-project (publica y empaqueta). --stub es opcional; si se omite, el packer descarga el stub que corresponda desde GitHub Releases.
  - Stub Avalonia creado (esqueleto) en src/DotnetPackaging.Exe.Installer con lector de payload.
  - Instalador: opciÛn de crear acceso directo en Escritorio en el paso Finish; acceso directo en Start Menu se mantiene.
  - CI: publica stubs win-x64 y win-arm64 como single-file self-extract con hashes y los sube a un GitHub Release (tag v{SemVer}).
  - Packer: logging antes de descargar el stub para informar del tiempo de espera.
- Next:
  - UI: Integrar SlimWizard de Zafiro en el stub (ahora hay UI mÌnima). NavegaciÛn con WizardNavigator y p·ginas.
  - LÛgica: ElevaciÛn UAC y carpeta por defecto en Program Files seg˙n arquitectura.
  - Packer: cachÈ local de stubs y reintentos/validaciÛn de hashes.
  - DetecciÛn avanzada de ejecutable e icono (paridad con .deb/.appimage).
  - Modo silencioso.
  - Pruebas E2E en Windows.

---

## CRITICAL UNRESOLVED ISSUE: Uninstaller Crash (2025-11-20)

### Problem Summary
The Windows uninstaller (Uninstall.exe) crashes with access violation **before any managed code executes**.

**Error Signature:**
- Exception: 0xc0000005 (Access Violation)
- Exit Code: 0x80131506 (.NET Runtime internal error)
- Crash Offset: 0x00000000000b24f6 (consistent)
- PE Timestamp: 0x68ffe47c (consistent despite rebuilds)
- Assembly Version: 2.0.0.0

**Critical Fact:** Crash occurs BEFORE Program.Main - no managed code runs at all.

### Attempted Fixes (All Failed)
1. Centralized Serilog logging to %TEMP%\DotnetPackaging.Installer\
2. Removed Win32 P/Invoke (FindResource, LoadResource)
3. Fixed Avalonia Dispatcher (base.OnFrameworkInitializationCompleted, Dispatcher.UIThread.Post)
4. Rename strategy for locked directories
5. Non-deterministic builds (Deterministic=false, AssemblyVersion=2.0.0.0)
6. Multiple complete clean rebuilds (bin/obj/temp cache)
7. Emergency logging at top of Program.Main (does not execute)

### Key Evidence
- Installer works perfectly
- Uninstaller is IDENTICAL binary (created via File.Copy)
- Same crash offset across all builds
- Same PE timestamp despite forced non-deterministic builds
- Emergency log (%TEMP%\dp-emergency.log) is never created = crash before Program.Main

### Hypothesis
Crash during .NET Runtime initialization or native DLL loading, NOT in managed code.
Possibly Single-File bundling corruption when executable is copied.

### Diagnostic Steps for Next Agent

1. **Check if crash is before Main:**
   `powershell
   Get-Content C:\Users\JMN\AppData\Local\Temp\dp-emergency.log
   `
   If empty/missing ‚Üí crash is definitely before managed entry point

2. **Use WinDbg:**
   Attach to Uninstall.exe and identify what module owns offset 0x00000000000b24f6

3. **Compare binaries:**
   `powershell
   dumpbin /headers C:\Users\JMN\Desktop\AngorSetup.exe > installer-headers.txt
   dumpbin /headers C:\Users\JMN\AppData\Local\Programs\AngorTest\Uninstall.exe > uninstaller-headers.txt
   Compare-Object (Get-Content installer-headers.txt) (Get-Content uninstaller-headers.txt)
   `

4. **Try without Single-File:**
   Modify DotnetPackaging.Exe.Installer.csproj:
   - Remove or set PublishSingleFile=false
   - Test if crash persists

5. **Alternative approaches:**
   - Don't copy installer - registry points to original with --uninstall flag
   - Build uninstaller as separate project (not copy)
   - Generate PowerShell uninstaller script instead

### Code Locations
- src/DotnetPackaging.Exe.Installer/Program.cs:15 - Emergency log (never executes)
- src/DotnetPackaging.Exe.Installer/Core/Installer.cs:39 - File.Copy creates uninstaller
- src/DotnetPackaging.Exe.Installer/App.axaml.cs:32 - base.OnFrameworkInitializationCompleted
- src/DotnetPackaging.Exe.Installer/Core/LoggerSetup.cs - Logging config

### Test Command
`powershell
# Generate installer
dotnet run --project src\DotnetPackaging.Tool\DotnetPackaging.Tool.csproj -- exe from-project --project F:\Repos\angor\src\Angor\Avalonia\AngorApp.Desktop\AngorApp.Desktop.csproj --output C:\Users\JMN\Desktop\AngorSetup.exe --rid win-x64

# Install to AngorTest, then run uninstaller
C:\Users\JMN\AppData\Local\Programs\AngorTest\Uninstall.exe --uninstall
`

### Environment
- Windows 11
- .NET 8.0.22
- Avalonia 11.x
- Single-file self-contained deployment

**CONFIRMED (2025-11-20 13:37):** dp-emergency.log created during install, NOT during uninstall.
Crash is definitively BEFORE Program.Main. Problem is in .NET Runtime init or native DLL loading.

### BREAKTHROUGH (2025-11-20 15:08)

**ROOT CAUSE FOUND:** Problem is WHERE the executable runs from, NOT the binary itself.

- Desktop\AngorSetup.exe --uninstall ‚Üí ‚úÖ WORKS
- Programs\AngorTest\Uninstall.exe --uninstall ‚Üí ‚ùå CRASHES
- Both files IDENTICAL (SHA256 verified)

**Conclusion:** .NET Single-File runtime fails to extract from installation directory.

**FIX:** Don't copy uninstaller. Registry should point to Desktop installer with --uninstall flag.
Or: Extract uninstaller to %TEMP% before running.

### UPDATE (2025-11-20 15:27)

**SOLUTION IMPLEMENTED:** Uninstaller now copies to %TEMP%
- Modified Installer.cs:RegisterUninstaller() to copy uninstaller to:
  %TEMP%\DotnetPackaging\Uninstallers\{appId}\Uninstall.exe
- Registry UninstallString now points to %TEMP% location
- This avoids .NET Single-File extraction issues in installation directory

**NEW BLOCKER:** Avalonia Dispatcher crash prevents testing
After implementing %TEMP% solution, installer crashes with:
System.InvalidOperationException: Cannot perform requested operation because the Dispatcher shut down

**Root cause:** Avalonia Dispatcher closes before async wizard completes.

**Attempted fixes (ALL FAILED):**
1. Dispatcher.UIThread.Post - async void doesn't wait
2. Dispatcher.UIThread.InvokeAsync - same issue  
3. desktopLifetime.Startup event - async void
4. mainWindow.Opened event - async void
5. ShutdownMode.OnMainWindowClose - closes too early
6. MainWindow in OnFrameworkInitializationCompleted - same

**Current state:**
- Installer.cs: ‚úÖ %TEMP% copy implemented
- App.axaml.cs: ‚ùå Dispatcher shutdown issue
- Cannot test %TEMP% solution until Dispatcher is fixed

**Next steps:**
- Fix Avalonia async initialization pattern
- OR revert to working version and implement %TEMP% solution there
- OR use synchronous UI initialization then async operations

See WARP.md line 356 for full details.

### FINAL SOLUTION (2025-11-20 16:15) ‚úÖ

**Problem Solved:** Uninstaller crashes when executed from installation directory.

**Root Cause:** .NET Single-File runtime extraction fails in locked/restricted directories.

**Solution Implemented:** Native Launcher Pattern
- Created UninstallLauncher.exe (WinExe, ~12MB, single-file .NET 8)
- Launcher copies Uninstall.exe to %TEMP% and executes it
- Registry points to launcher (stable location in install dir)
- Launcher waits for uninstaller to complete (WaitForExit)

**Key Files:**
- src/DotnetPackaging.Exe.UninstallLauncher/ - New launcher project
- src/DotnetPackaging.Exe.Installer/Core/Installer.cs - Extracts launcher from resources
- src/DotnetPackaging.Exe.Installer/Resources/UninstallLauncher.exe - Embedded binary

**Why It Works:**
‚úÖ Launcher always available (embedded in installer)
‚úÖ Uninstaller always runs from %TEMP% (no extraction issues)
‚úÖ No console window (WinExe)
‚úÖ No Windows error dialog (WaitForExit)
‚úÖ Robust to %TEMP% cleanup (launcher recreates on each run)

**Future Improvements (see WARP.md lines 481-618):**
1. Native AOT or C++ launcher (reduce from 12MB to <1MB)
2. Download launcher from GitHub releases (smaller installer)
3. Self-deleting temp uninstaller
4. Retry logic for antivirus locks

**Testing:**
‚úÖ Install/uninstall works correctly
‚úÖ No console window appears
‚úÖ No Windows error dialogs
‚úÖ Uninstaller UI shows properly
‚úÖ Works after multiple cycles

See WARP.md lines 383-618 for complete documentation.
