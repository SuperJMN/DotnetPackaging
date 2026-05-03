# Roadmap: Project Context + Shared Publish Packaging

## Goal

Keep the useful behavior of `from-project` (project metadata, inferred executable, vendor, license, URL, terminal mode, etc.) while allowing higher-level tools such as DotnetDeployer to publish once and package several formats from the same publish output.

DotnetPackaging remains responsible for packaging primitives and project-aware packaging conveniences. It must not learn about DotnetDeployer, Fleet, releases, workers, batches, or remote artifact storage.

## Current State

- `from-directory` packages an already-published directory.
- `from-project` reads `.csproj` metadata, runs `dotnet publish`, then packages the output.
- `ProjectMetadataReader` already reads the metadata needed for good package defaults.
- `ProjectMetadataDefaults.ResolveFromProject(...)` already converts project metadata into package defaults.
- The missing piece is a reusable public abstraction that lets callers reuse those project defaults without forcing one `dotnet publish` per package format.

## Target Design

Introduce a project packaging context API:

```csharp
var context = ProjectPackagingContext.FromProject(projectPath, logger);
var publish = await publisher.Publish(context.CreatePublishRequest(rid, configure));
var debOptions = context.ResolveFromDirectoryOptions(overrides);
var deb = await debPackager.Pack(publish.Container, debOptions, logger);
```

The context should expose:

- `ProjectPath`
- `ProjectFile`
- `ProjectMetadata`
- `InferExecutableName()`
- `ResolveFromDirectoryOptions(FromDirectoryOptions overrides)`
- format-specific helpers only where the generic options are not enough, for example EXE/DMG metadata.

`from-project` should stay as a public convenience API, but internally become a thin wrapper over:

1. Create project context.
2. Publish project.
3. Package published directory with metadata-aware options.

## Implementation Phases

### Phase 1: Extract Context Without Behavior Change

- Add `ProjectPackagingContext` in the core `DotnetPackaging` project.
- Move the project metadata/default-resolution behavior behind the context.
- Keep `ProjectMetadataReader` and `ProjectMetadataDefaults` public and backward-compatible.
- Add tests proving that context resolution preserves current defaults:
  - description from `Description`
  - maintainer from `Authors`
  - vendor from `Company`
  - license from `PackageLicenseExpression`
  - URL from `PackageProjectUrl` or `RepositoryUrl`
  - executable from `AssemblyName` or project filename
  - terminal mode from `OutputType`

### Phase 2: Rebuild `from-project` on the Context

- Refactor each `*PackagerExtensions.FromProject(...)` to use `ProjectPackagingContext`.
- Keep all existing method signatures.
- Keep `PackProject(...)` as the disk-writing convenience wrapper.
- Verify existing CLI commands still behave the same.

### Phase 3: Add Explicit Shared-Publish APIs

- Add APIs that accept a published container plus project context.
- Prefer clear names over ambiguous overloads, for example:
  - `PackPublishedProject(...)`
  - `ResolvePublishedProjectOptions(...)`
- Keep `from-directory` as a metadata-free primitive unless metadata is explicitly supplied.

### Phase 4: Documentation and Examples

- Document three supported paths:
  - `from-directory`: package a directory, caller supplies metadata.
  - `from-project`: publish and package one format.
  - shared publish: publish once, package several formats with project metadata.
- Add examples for Deployer-style usage.

## Acceptance Criteria

- Existing `dotnetpackager <format> from-project` commands remain source- and behavior-compatible.
- A caller can package two formats from one publish output without losing project metadata defaults.
- No dependency is introduced from DotnetPackaging to DotnetDeployer or DotnetFleet.
- Existing tests pass, and new tests cover metadata preservation through the context.

## Manual NuGet Publishing When GitHub Is Unavailable

Use this path when GitHub Releases cannot be created but NuGet is available.

```bash
cd /mnt/fast/Repos/DotnetPackaging

VERSION=1.2.3
OUT=./artifacts/nuget-manual

dotnet restore
dotnet build -c Release -p:ContinuousIntegrationBuild=true -p:Version=$VERSION --no-restore
dotnet pack -c Release --no-build -p:IncludeSymbols=false -p:SymbolPackageFormat=snupkg -p:Version=$VERSION -o "$OUT"

find "$OUT" -name '*.nupkg' ! -name '*.symbols.nupkg' -print0 |
  xargs -0 -I{} dotnet nuget push "{}" \
    --api-key "$NUGET_API_KEY" \
    --source https://api.nuget.org/v3/index.json \
    --skip-duplicate
```

Notes:

- Set `NUGET_API_KEY` in the shell; do not write it to disk.
- Use the same `VERSION` for every package in a coordinated release.
- GitHub release assets, such as Windows stubs, can be generated and uploaded later when GitHub is available again.

