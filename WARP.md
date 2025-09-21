# WARP.md

This file guides Warp (and future contributors) on how CI/CD works in this repository.

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
