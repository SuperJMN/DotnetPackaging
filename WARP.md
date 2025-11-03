# WARP.md

This file guides Warp (and future contributors) on how CI/CD and packaging work in this repository.

Scope: whole repository (DotnetPackaging).

CI pipeline (Azure Pipelines)
- Definition: azure-pipelines.yml at repo root.
- Agent: ubuntu-latest.
- Tools: installs DotnetDeployer.Tool globally.
- Behavior by branch:
  - master: packs and pushes all packable projects to NuGet via dotnetdeployer nuget --api-key $(NuGetApiKey)
  - other branches and PRs: dry run (packs but does not push) via --no-push
- Packable projects: every project with IsPackable/PackAsTool set. This includes the CLI tool (src/DotnetPackaging.Console), since PackAsTool=true and it is part of the solution.

Versioning (GitVersion)
- DotnetDeployer determines the version primarily using GitVersion (NuGetVersion or MajorMinorPatch).
- If GitVersion is unavailable, the tool falls back to git describe --tags --long and converts it to a NuGet-compatible version.
- Practical effect: merging a PR into master automatically triggers a publish with the GitVersion-computed version.

Secrets
- The pipeline expects a variable group named api-keys providing:
  - NuGetApiKey: API key used by dotnetdeployer to push packages.
- Do not hardcode secrets. Locally, export environment variables and pass them to the tool.

Local replication
- Install tool: dotnet tool install --global DotnetDeployer.Tool
- Dry run (no push): dotnetdeployer nuget --api-key "$NUGET_API_KEY" --no-push
- Real publish (imitates master): dotnetdeployer nuget --api-key "$NUGET_API_KEY"

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
- MSIX (Windows)
  - Status: experimental/preview. Library: src/DotnetPackaging.Msix with tests in src/DotnetPackaging.Msix.Tests.
  - Validation: tests unpack resulting MSIX using makeappx tooling in CI-like conditions; end-to-end CLI exposure pending (see TODOs below).
- Flatpak (Linux)
  - Status: supported (bundle via system `flatpak`) with internal OSTree bundler fallback.
  - Libraries: src/DotnetPackaging.Flatpak (Factory, Packer, OSTree scaffolding).
  - How it works: builds a Flatpak layout (metadata at root, files/ subtree) from a publish directory; icons auto-detected and installed under files/share/icons/.../apps/<appId>.(svg/png). Desktop Icon is forced to <appId>.
  - Bundling: prefers system `flatpak build-export/build-bundle`; if not available or fails, uses internal bundler to emit a single-file `.flatpak` (unsigned, for testing).
  - Defaults: freedesktop runtime 24.08 (runtime/sdk), branch=stable, common permissions (network/ipc, wayland/x11/pulseaudio, dri, filesystem=home). Command defaults to AppId.

CLI tool (dotnet tool)
- Project: src/DotnetPackaging.Console (PackAsTool=true, ToolCommandName=dotnetpackaging).
- Commands available:
  - appimage: create an AppImage from a directory (typically dotnet publish output). Autodetects executable + architecture; generates metadata and icons.
  - deb: create a .deb from a directory (dotnet publish output). Detects executable; generates metadata, .desktop and wrapper.
  - flatpak: layout, bundle (system or internal), repo, and pack (minimal UX).
- Common options (all commands share a metadata set):
  - --directory <dir> (required): input directory to package from.
  - --output <file> (required): output file (.AppImage or .deb).
  - --application-name, --wm-class, --main-category, --additional-categories, --keywords, --comment, --version,
    --homepage, --license, --screenshot-urls, --summary, --appId, --executable-name, --is-terminal, --icon <path>.
- Examples (from a published folder):
  - AppImage: dotnetpackaging appimage --directory /path/to/publish --output /path/out/MyApp.AppImage --application-name "MyApp"
  - Deb:      dotnetpackaging deb      --directory /path/to/publish --output /path/out/myapp_1.0.0_amd64.deb --application-name "MyApp"
  - Flatpak (minimal): dotnetpackager flatpak pack --directory /path/to/publish --output-dir /path/out
  - Flatpak (bundle):  dotnetpackager flatpak bundle --directory /path/to/publish --output /path/out/MyApp.flatpak --system

Tests
- AppImage tests (test/DotnetPackaging.AppImage.Tests):
  - CreateAppImage validates building from containers and saving bytes.
  - SquashFS tests ensure filesystem construction integrity.
- Deb tests (test/DotnetPackaging.Deb.Tests):
  - Integration tests covering metadata and tar entries layout.
- MSIX tests (src/DotnetPackaging.Msix.Tests):
  - Validate building MSIX and unpacking with makeappx to assert structure.
- Gaps / TODOs:
  - Add CLI end-to-end tests (invocation of dotnetpackaging appimage/deb on temp publishes and validating outputs).
  - Integrate dotnet test into azure-pipelines.yml (currently only packaging/publish runs).
  - Expose msix in CLI and add corresponding tests.

Developer workflow tips
- Publish input
  - AppImage/Deb consume a folder produced by dotnet publish. Both framework-dependent and self-contained outputs are supported; self-contained tends to be more robust (fewer runtime surprises), but produces larger images.
  - For AppImage, ensure an ELF executable is present (self-contained single-file publish is acceptable). If not specified, the first eligible ELF is chosen.
- Icon handling
  - The CLI and libraries attempt to discover icons automatically. You can override via --icon or supply common names in the root (icon.svg, icon-256.png, icon.png).
- Debug
  - Set DOTNETPACKAGING_DEBUG=1 to dump AppImage intermediate artifacts (runtime + squashfs).

Repository map (relevant)
- src/DotnetPackaging.AppImage: AppImage core (AppImageFactory, RuntimeFactory, SquashFS).
- src/DotnetPackaging.Deb: Debian packaging (Tar entries, DebFile).
- src/DotnetPackaging.Msix: MSIX packaging (builder and helpers).
- src/DotnetPackaging.Console: CLI (dotnet tool) with commands appimage and deb.
- test/*: AppImage and Deb tests; src/DotnetPackaging.Msix.Tests for MSIX validation.

Backlog / Future work
- Expose msix as a first-class command in the CLI.
- Add CLI E2E tests and hook dotnet test in CI.
- Optional: enrich icon detection strategies and metadata mapping (e.g., auto-appId from name + reverse DNS).
- Optional: support additional formats (e.g., rpm) if needed.
